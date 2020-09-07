﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Pkcs;
using Certify.Models;
using Certify.Models.Config;
using Certify.Models.Plugins;
using Certify.Models.Providers;

using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;

namespace Certify.Providers.ACME.Certes
{
    /// <summary>
    /// Certes Provider settings for serialization
    /// </summary>
    public class CertesSettings
    {
        public string AccountEmail { get; set; }
        public string AccountUri { get; set; }
        public string AccountKey { get; set; }
    }

#pragma warning disable IDE1006 // Naming Styles
    public class DiagEcKey
    {
        public string kty { get; set; }
        public string crv { get; set; }
        public string x { get; set; }
        public string y { get; set; }
    }
#pragma warning restore IDE1006 // Naming Styles

    // used to diagnose account key faults
    public class DiagAccountInfo
    {
        public int ID { get; set; }
        public DiagEcKey Key { get; set; }
    }

    public class LoggingHandler : DelegatingHandler
    {
        public DiagAccountInfo DiagAccountInfo { get; set; }
        private ILog _log = null;

        public LoggingHandler(HttpMessageHandler innerHandler, ILog log)
            : base(innerHandler)
        {
            _log = log;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_log != null)
            {
                _log.Debug($"Http Request: {request}");
                if (request.Content != null)
                {
                    _log.Debug(await request.Content.ReadAsStringAsync());
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (_log != null)
            {
                _log.Debug($"Http Response: {response}");

                if (response.Content != null)
                {
                    _log.Debug(await response.Content.ReadAsStringAsync());
                }
            }

            return response;
        }
    }


    /// <summary>
    /// ACME Provider using certes https://github.com/fszlin/certes
    /// </summary>
    public class CertesACMEProvider : IACMEClientProvider
    {
        private AcmeContext _acme;

        private Uri _serviceUri = null;

        private readonly string _settingsFolder = null;

        private CertesSettings _settings = null;
        private Dictionary<string, IOrderContext> _currentOrders;
        private IdnMapping _idnMapping = new IdnMapping();
        private DateTime _lastInitDateTime = new DateTime();
        private readonly bool _newContactUseCurrentAccountKey = false;

        private AcmeHttpClient _httpClient;
        private LoggingHandler _loggingHandler;

        private readonly string _userAgentName = "Certify SSL Manager";
        private ILog _log = null;

        private List<byte[]> _issuerCertCache = new List<byte[]>();

        private ACMECompatibilityMode _compatibilityMode = ACMECompatibilityMode.Standard;

        public CertesACMEProvider(string acmeBaseUri, string settingsPath, string userAgentName, bool allowInvalidTls = false)
        {
            _settingsFolder = settingsPath;

            var certesAssembly = typeof(AcmeContext).Assembly.GetName();

            _userAgentName = $"{userAgentName} {certesAssembly.Name}/{certesAssembly.Version}";

            _serviceUri = new Uri(acmeBaseUri);


#pragma warning disable SCS0004 // Certificate Validation has been disabled
            if (allowInvalidTls)
            {
                ServicePointManager.ServerCertificateValidationCallback += (obj, cert, chain, errors) =>
                {
                    // ignore all cert errors when validating URL response
                    return true;
                };
            }
#pragma warning restore SCS0004 // Certificate Validation has been disabled

            this.RefreshIssuerCertCache();
        }

        public string GetProviderName() => "Certes";

        public string GetAcmeBaseURI() => _serviceUri?.ToString();

        public async Task<Uri> GetAcmeTermsOfService()
        {

            if (_acme == null)
            {
                // no acme context setup yet (account not yet initialised), create a temporary context
                PreInitAcmeContext();
                _acme = new AcmeContext(_serviceUri, null, _httpClient);
            }

            return await _acme.TermsOfService();
        }

        /// <summary>
        /// setup the basic settings before we init the acme context
        /// </summary>
        /// <param name="acmeDirectoryUrl"></param>
        private void PreInitAcmeContext()
        {
            _lastInitDateTime = DateTime.Now;

            _loggingHandler = new LoggingHandler(new HttpClientHandler(), _log);
            var customHttpClient = new System.Net.Http.HttpClient(_loggingHandler);
            if (customHttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                customHttpClient.DefaultRequestHeaders.Remove("User-Agent");
            }
            customHttpClient.DefaultRequestHeaders.Add("User-Agent", _userAgentName);

#if DEBUG
            //  customHttpClient.Timeout = TimeSpan.FromSeconds(10);
#endif

            _httpClient = new AcmeHttpClient(_serviceUri, customHttpClient);
        }

        /// <summary>
        /// Initialise provider settings, loading current account key if present
        /// </summary>
        public async Task<bool> InitProvider(ILog log = null, AccountDetails account = null)
        {
            if (log != null)
            {
                _log = log;
            }

            PreInitAcmeContext();

            if (_settings == null)
            {
                if (account == null)
                {
                    // if initalising without a known account, attempt to load details from storage
                    var settingsFilePath = Path.Combine(_settingsFolder, "c-settings.json");
                    if (File.Exists(settingsFilePath))
                    {
                        var json = System.IO.File.ReadAllText(settingsFilePath);
                        _settings = Newtonsoft.Json.JsonConvert.DeserializeObject<CertesSettings>(json);
                    }
                    else
                    {
                        _settings = new CertesSettings();
                    }

                    if (!string.IsNullOrEmpty(_settings.AccountKey))
                    {
                        if (System.IO.File.Exists(Path.Combine(_settingsFolder, "c-acc.key")))
                        {
                            //remove legacy key info
                            System.IO.File.Delete(Path.Combine(_settingsFolder, "c-acc.key"));
                        }
                        SetAcmeContextAccountKey(_settings.AccountKey);
                    }
                    else
                    {
                        // no account key in settings, check .key (legacy key file)
                        if (System.IO.File.Exists(Path.Combine(_settingsFolder, "c-acc.key")))
                        {
                            var pem = System.IO.File.ReadAllText(Path.Combine(_settingsFolder, "c-acc.key"));
                            SetAcmeContextAccountKey(pem);
                        }
                    }
                }
                else
                {
                    _settings = new CertesSettings
                    {
                        AccountEmail = account.Email,
                        AccountKey = account.AccountKey,
                        AccountUri = account.AccountURI
                    };
                    SetAcmeContextAccountKey(_settings.AccountKey);
                }
            }
            else
            {
                SetAcmeContextAccountKey(_settings.AccountKey);
            }

            _currentOrders = new Dictionary<string, IOrderContext>();

            return await Task.FromResult(true);
        }

        private async Task<string> CheckAcmeAccount()
        {
            // check our current account ID and key match the values LE expects
            if (_acme == null)
            {
                return "none";
            }

            try
            {
                var accountContext = await _acme.Account();
                var account = await accountContext.Resource();

                if (account.Status == AccountStatus.Valid)
                {
                    if (account.TermsOfServiceAgreed == false)
                    {
                        return "tos-required";
                    }
                    else
                    {
                        // all good
                        return "ok";
                    }
                }
                else
                {
                    if (account.Status == AccountStatus.Revoked)
                    {
                        return "account-revoked";
                    }

                    if (account.Status == AccountStatus.Deactivated)
                    {
                        return "account-deactivated";
                    }
                }

                return "unknown";
            }
            catch (AcmeRequestException exp)
            {
                if (exp.Error.Type == "urn:ietf:params:acme:error:accountDoesNotExist")
                {
                    return "account-doesnotexist";
                }
                else
                {
                    return "account-error";
                }
            }
            catch (Exception)
            {
                // we failed to check the account status, probably because of connectivity. Assume OK
                return "ok";
            }
        }


        public async Task<bool> DeactivateAccount(ILog log)
        {
            var acc = await _acme.Account();
            var result = await acc.Deactivate();

            if (result.Status == AccountStatus.Deactivated)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> UpdateAccount(ILog log, string email, bool termsAgreed)
        {
            var acc = await _acme.Account();

            var results = await acc.Update(new string[] { email }, termsAgreed);
            if (results.Status == AccountStatus.Valid)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> ChangeAccountKey(ILog log)
        {
            if (_acme == null)
            {
                log?.Error("No account context. Cannot update account key.");

                return false;
            }
            else
            {
                // allocate new key and inform LE of key change
                // same default key type as certes
                var newKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

                await _acme.ChangeKey(newKey);

                await PopulateSettingsFromCurrentAccount();

                return true;
            }
        }

        private async Task PopulateSettingsFromCurrentAccount()
        {
            var pem = _acme.AccountKey.ToPem();

            _settings.AccountKey = pem;
            _settings.AccountUri = (await _acme.Account()).Location.ToString();
        }

        private async Task<bool> WriteAllTextAsync(string path, string content)
        {
            try
            {
                using (var fs = File.CreateText(path))
                {
                    await fs.WriteAsync(content);
                    await fs.FlushAsync();
                }

                // artificial delay for flush to really complete (just begin superstitious)
                await Task.Delay(250);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determine if we have a currently registered account with the ACME CA (e.g. Let's Encrypt)
        /// </summary>
        /// <returns>  </returns>
        public bool IsAccountRegistered()
        {
            if (!string.IsNullOrEmpty(_settings.AccountEmail))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Set a new account key from PEM encoded text
        /// </summary>
        /// <param name="pem">  </param>
        private void SetAcmeContextAccountKey(string pem)
        {
            var accountkey = KeyFactory.FromPem(pem);

            _acme = new AcmeContext(_serviceUri, accountkey, _httpClient);

            if (_settings.AccountKey != pem)
            {
                _settings.AccountKey = pem;
            }
        }

        public AccountDetails GetCurrentAcmeAccount()
        {
            if (!string.IsNullOrEmpty(_settings.AccountUri))
            {
                return new AccountDetails
                {
                    ID = _settings.AccountUri.Split('/').Last(),
                    AccountKey = _settings.AccountKey,
                    AccountURI = _settings.AccountUri,
                    Email = _settings.AccountEmail
                };
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Register a new account with the ACME CA (e.g. Let's Encrypt), accepting terms and conditions
        /// </summary>
        /// <param name="log">  </param>
        /// <param name="email">  </param>
        /// <returns>  </returns>
        public async Task<ActionResult<AccountDetails>> AddNewAccountAndAcceptTOS(ILog log, string email)
        {
            try
            {
                IKey accKey = null;

                if (_newContactUseCurrentAccountKey && !string.IsNullOrEmpty(_settings.AccountKey))
                {
                    accKey = KeyFactory.FromPem(_settings.AccountKey);
                }

                // start new account context, create new account (with new key, if not enabled)
                _acme = new AcmeContext(_serviceUri, accKey, _httpClient);
                var account = await _acme.NewAccount(email, true);

                _settings.AccountEmail = email;

                await PopulateSettingsFromCurrentAccount();

                // archive account key and update current settings with new ACME account key and account URI
                // var keyUpdated = ArchiveAccountKey(account);
                // var settingsSaved = await SaveSettings();

                //  if (keyUpdated && settingsSaved)
                {
                    log?.Information($"Registering account {email} with certificate authority");

                    // re-init provider based on new account key
                    // await InitProvider(acmeApiEndpoint, _log);

                    return new ActionResult<AccountDetails>
                    {
                        IsSuccess = true,
                        Result = new AccountDetails
                        {
                            AccountKey = _settings.AccountKey,
                            Email = _settings.AccountEmail,
                            AccountURI = _settings.AccountUri,
                            ID = _settings.AccountUri.Split('/').Last()
                        }
                    };
                }
                /* else
                 {
                     throw new Exception($"Failed to save account settings: keyUpdate:{keyUpdated} settingsSaved:{settingsSaved}");
                 }*/
            }
            catch (Exception exp)
            {
                log.Error($"Failed to register account with certificate authority: {exp.Message}");
                return new ActionResult<AccountDetails> { IsSuccess = false, Message = $"Failed to register account with certificate authority: {exp.Message}" };
            }
        }

        /// <summary>
        /// Begin order for new certificate for one or more domains, fetching the required challenges
        /// to complete
        /// </summary>
        /// <param name="log">  </param>
        /// <param name="config">  </param>
        /// <param name="orderUri"> Uri of existing order to resume </param>
        /// <returns>  </returns>
        public async Task<PendingOrder> BeginCertificateOrder(ILog log, CertRequestConfig config, string orderUri = null)
        {
            if (DateTime.Now.Subtract(_lastInitDateTime).TotalMinutes > 30)
            {
                // our acme context is stale, start a new one
                await InitProvider(_log);
            }

            var pendingOrder = new PendingOrder { IsPendingAuthorizations = true };

            // prepare a list of all pending authorization we need to complete, or those we have
            // already satisfied
            var authzList = new List<PendingAuthorization>();

            //if no alternative domain specified, use the primary domain as the subject
            var domainOrders = new List<string>
            {
                // order all of the distinct domains in the config (primary + SAN).
                _idnMapping.GetAscii(config.PrimaryDomain)
            };

            if (config.SubjectAlternativeNames != null)
            {
                foreach (var s in config.SubjectAlternativeNames)
                {
                    if (!domainOrders.Contains(s))
                    {
                        domainOrders.Add(_idnMapping.GetAscii(s));
                    }
                }
            }

            try
            {
                IOrderContext order = null;
                var remainingAttempts = 3;
                var orderCreated = false;
                object lastException = null;
                var orderErrorMsg = "";

                try
                {
                    while (!orderCreated && remainingAttempts > 0)
                    {
                        try
                        {
                            remainingAttempts--;

                            log.Information($"BeginCertificateOrder: creating/retrieving order. Retries remaining:{remainingAttempts} ");

                            if (orderUri != null)
                            {
                                order = _acme.Order(new Uri(orderUri));
                            }
                            else
                            {
                                order = await _acme.NewOrder(domainOrders);
                            }

                            if (order != null)
                            {
                                orderCreated = true;
                            }
                        }
                        catch (Exception exp)
                        {
                            log.Error(exp.ToString());

                            orderErrorMsg = exp.Message;

                            if (exp is TaskCanceledException)
                            {
                                log.Warning($"BeginCertificateOrder: timeout while communicating with the ACME API");
                            }

                            if (exp is AcmeRequestException)
                            {
                                var err = (exp as AcmeRequestException).Error;

                                // e.g. urn:ietf:params:acme:error:userActionRequired

                                orderErrorMsg = err?.Detail ?? orderErrorMsg;

                                if ((int)err.Status == 429)
                                {
                                    // hit an ACME API rate limit 

                                    log.Warning($"BeginCertificateOrder: encountered a rate limit while communicating with the ACME API");

                                    return new PendingOrder(orderErrorMsg);
                                }

                                if (err.Type?.EndsWith("accountDoesNotExist") == true)
                                {
                                    // wrong account details, probably used staging for prod or vice versa
                                    log.Warning($"BeginCertificateOrder: attempted to use invalid account details with the ACME API");

                                    return new PendingOrder(orderErrorMsg);

                                }

                            }
                            else if (exp.InnerException != null && exp.InnerException is AcmeRequestException)
                            {
                                orderErrorMsg = (exp.InnerException as AcmeRequestException).Error?.Detail ?? orderErrorMsg;
                            }

                            remainingAttempts--;

                            log.Error($"BeginCertificateOrder: error creating order. Retries remaining:{remainingAttempts} :: {orderErrorMsg} ");

                            lastException = exp;

                            if (remainingAttempts == 0)
                            {
                                // all attempts to create order failed
                                throw;
                            }
                            else
                            {
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (NullReferenceException exp)
                {
                    var msg = $"Failed to begin certificate order (account problem or API is not currently available): {exp.Message}";

                    log.Error(msg);

                    return new PendingOrder(msg);
                }

                if (order == null || order.Location == null)
                {

                    var msg = "Failed to begin certificate order.";

                    if (lastException is AcmeRequestException)
                    {
                        var err = (lastException as AcmeRequestException).Error;

                        msg = err?.Detail ?? msg;
                        if (lastException != null && (lastException as Exception).InnerException is AcmeRequestException)
                        {
                            msg = ((lastException as Exception).InnerException as AcmeRequestException).Error?.Detail ?? msg;
                        }
                    }
                    else
                    {
                        if (lastException is Exception)
                        {
                            msg += "::" + (lastException as Exception).ToString();
                        }
                    }

                    return new PendingOrder("Error creating Order with Certificate Authority: " + msg);

                }

                orderUri = order.Location.ToString();

                pendingOrder.OrderUri = orderUri;

                log.Information($"Created ACME Order: {orderUri}");

                // track order in memory, keyed on order Uri
                if (_currentOrders.Keys.Contains(orderUri))
                {
                    _currentOrders.Remove(orderUri);
                }

                _currentOrders.Add(orderUri, order);

                // handle order status 'Ready' if all authorizations are already valid
                bool requireAuthzFetch = true;
                var orderDetails = await order.Resource();

                if (orderDetails.Status == OrderStatus.Ready)
                {
                    pendingOrder.IsPendingAuthorizations = false;
                    requireAuthzFetch = true;
                }

                if (_compatibilityMode == ACMECompatibilityMode.Standard)
                {
                    if (orderDetails.Status == OrderStatus.Valid)
                    {
                        pendingOrder.IsPendingAuthorizations = false;
                        requireAuthzFetch = true;
                    }
                }

                if (requireAuthzFetch)
                {
                    // get all required pending (or already valid) authorizations for this order

                    log.Information($"Fetching Authorizations.");

                    var orderAuthorizations = await order.Authorizations();

                    // get the challenges for each authorization
                    foreach (var authz in orderAuthorizations)
                    {
                        log.Debug($"Fetching Authz Challenges.");

                        var allChallenges = await authz.Challenges();
                        var res = await authz.Resource();
                        var authzDomain = res.Identifier.Value;
                        if (res.Wildcard == true)
                        {
                            authzDomain = "*." + authzDomain;
                        }

                        var challenges = new List<AuthorizationChallengeItem>();

                        // determine if we are interested in each challenge type before fetching the challenge details, some APIs hang when you fetch a validated auth
                        var includeHttp01 = true;
                        var includeDns01 = true;

                        if (_compatibilityMode == ACMECompatibilityMode.AltProvider1)
                        {
                            if (config.Challenges?.Any(c => c.ChallengeType == SupportedChallengeTypes.CHALLENGE_TYPE_HTTP) != true)
                            {
                                includeHttp01 = false;
                            }

                            if (config.Challenges?.Any(c => c.ChallengeType == SupportedChallengeTypes.CHALLENGE_TYPE_DNS) != true)
                            {
                                includeDns01 = false;
                            }

                            if (includeDns01 == false && includeHttp01 == false)
                            {
                                // if neither challenge is enabled, use both
                                includeHttp01 = true;
                                includeDns01 = true;
                            }
                        }

                        // add http challenge (if any)
                        if (includeHttp01)
                        {
                            var httpChallenge = await authz.Http();
                            if (httpChallenge != null)
                            {
                                try
                                {
                                    var httpChallengeStatus = await httpChallenge.Resource();

                                    log.Information($"Got http-01 challenge {httpChallengeStatus.Url}");

                                    if (httpChallengeStatus.Status == ChallengeStatus.Invalid)
                                    {
                                        log.Error($"HTTP challenge has an invalid status");
                                    }

                                    challenges.Add(new AuthorizationChallengeItem
                                    {
                                        ChallengeType = SupportedChallengeTypes.CHALLENGE_TYPE_HTTP,
                                        Key = httpChallenge.Token,
                                        Value = httpChallenge.KeyAuthz,
                                        ChallengeData = httpChallenge,
                                        ResourceUri = $"http://{authzDomain.Replace("*.", "")}/.well-known/acme-challenge/{httpChallenge.Token}",
                                        ResourcePath = $".well-known\\acme-challenge\\{httpChallenge.Token}",
                                        IsValidated = (httpChallengeStatus.Status == ChallengeStatus.Valid)
                                    });
                                }
                                catch (Exception exp)
                                {
                                    var msg = $"Could fetch http-01 challenge details from ACME server (timeout) : {exp.Message}";

                                    log.Error(msg);

                                    return new PendingOrder(msg);
                                }
                            }
                        }

                        // add dns challenge (if any)
                        if (includeDns01)
                        {
                            var dnsChallenge = await authz.Dns();
                            if (dnsChallenge != null)
                            {
                                var dnsChallengeStatus = await dnsChallenge.Resource();

                                log.Information($"Got dns-01 challenge {dnsChallengeStatus.Url}");

                                if (dnsChallengeStatus.Status == ChallengeStatus.Invalid)
                                {
                                    log.Error($"DNS challenge has an invalid status");
                                }

                                var dnsValue = _acme.AccountKey.DnsTxt(dnsChallenge.Token); //ComputeDnsValue(dnsChallenge, _acme.AccountKey);
                                var dnsKey = $"_acme-challenge.{authzDomain}".Replace("*.", "");

                                challenges.Add(new AuthorizationChallengeItem
                                {
                                    ChallengeType = SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                                    Key = dnsKey,
                                    Value = dnsValue,
                                    ChallengeData = dnsChallenge,
                                    IsValidated = (dnsChallengeStatus.Status == ChallengeStatus.Valid)
                                });
                            }
                        }

                        // report back on the challenges we now may need to attempt
                        authzList.Add(
                         new PendingAuthorization
                         {
                             Challenges = challenges,
                             Identifier = new IdentifierItem
                             {
                                 Dns = authzDomain,
                                 IsAuthorizationPending = !challenges.Any(c => c.IsValidated) //auth is pending if we have no challenges already validated
                             },
                             AuthorizationContext = authz,
                             IsValidated = challenges.Any(c => c.IsValidated),
                             OrderUri = orderUri
                         });
                    }

                    pendingOrder.Authorizations = authzList;
                }

                return pendingOrder;
            }
            catch (AcmeRequestException exp)
            {
                // failed to register one or more domain identifier with LE (invalid, rate limit or
                // CAA fail?)

                var msg = $"Could not begin certificate order: {exp.Error?.Detail}";

                log.Error(msg);

                return new PendingOrder(msg);
            }
        }

        private string GetExceptionMessage(Exception exp)
        {
            var msg = exp.Message;

            if (exp.InnerException != null)
            {
                if (exp.InnerException is AcmeRequestException)
                {
                    msg += ":: " + ((AcmeRequestException)exp.InnerException).Error.Detail;
                }
                else
                {
                    msg += ":: " + exp.InnerException.Message;
                }
            }

            return msg;
        }

        /// <summary>
        /// if not already validate, ask ACME CA to check we have answered the nominated challenges correctly
        /// </summary>
        /// <param name="log">  </param>
        /// <param name="challengeType">  </param>
        /// <param name="attemptedChallenge">  </param>
        /// <returns>  </returns>
        public async Task<StatusMessage> SubmitChallenge(ILog log, string challengeType, AuthorizationChallengeItem attemptedChallenge)
        {
            if (attemptedChallenge == null)
            {
                return new StatusMessage
                {
                    IsOK = false,
                    Message = "Challenge could not be submitted. No matching attempted challenge."
                };
            }

            if (!attemptedChallenge.IsValidated)
            {
                try
                {
                    await _acme.HttpClient.ConsumeNonce();
                }
                catch (Exception)
                {
                    return new StatusMessage
                    {
                        IsOK = false,
                        Message = "Failed to resume communication with Certificate Authority API. Try again later."
                    };
                }

                var challenge = (IChallengeContext)attemptedChallenge.ChallengeData;
                try
                {
                    var result = await challenge.Validate();

                    var maxAttempts = 10;
                    var attempts = maxAttempts;

                    while (attempts > 0 && (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing) && result.Error?.Detail == null)
                    {
                        log?.Warning($"Challenge response validation still pending. Re-checking [{attempts}]..");

                        // wait an increasing amount of time before checking again
                        var waitMs = 1000 + (((maxAttempts + 1) - attempts) * 500);
                        await Task.Delay(waitMs);

                        result = await challenge.Resource();

                        attempts--;
                    }

                    if (result.Status == ChallengeStatus.Valid)
                    {
                        return new StatusMessage
                        {
                            IsOK = true,
                            Message = "Submitted"
                        };
                    }
                    else
                    {
                        var defaultError = "Validation failed - unknown failure reason";

                        if (result.Status == ChallengeStatus.Pending)
                        {
                            defaultError = "Validation failed to complete within the time allowed.";
                        }

                        var msg = result.Error?.Detail ?? defaultError;

                        if (result.Error?.Subproblems?.Any() == true)
                        {
                            var subproblems = string.Join(", ", result.Error.Subproblems
                                .GroupBy(s => $"{s.Detail}:{s.Identifier}")
                                .Select(e => $"{e.FirstOrDefault().Identifier} : {e.FirstOrDefault().Detail}"));

                            msg = $"{result.Error?.Detail} :: {subproblems}";
                        }

                        return new StatusMessage
                        {
                            IsOK = false,
                            Message = msg
                        };
                    }
                }
                catch (AcmeRequestException exp)
                {
                    var msg = $"Submit Challenge failed: {exp.Error?.Detail}";

                    log.Error(msg);

                    return new StatusMessage
                    {
                        IsOK = false,
                        Message = msg
                    };
                }
            }
            else
            {
                return new StatusMessage
                {
                    IsOK = true,
                    Message = "Validated"
                };
            }
        }

        /// <summary>
        /// After we have asked the CA to check we have responded to the required challenges, check
        /// the result to see if they are now valid
        /// </summary>
        /// <param name="log">  </param>
        /// <param name="challengeType">  </param>
        /// <param name="pendingAuthorization">  </param>
        /// <returns>  </returns>
        public async Task<PendingAuthorization> CheckValidationCompleted(ILog log, string challengeType, PendingAuthorization pendingAuthorization)
        {
            var authz = (IAuthorizationContext)pendingAuthorization.AuthorizationContext;

            var res = await authz.Resource();

            var attempts = 20;
            while (attempts > 0 && (res.Status != AuthorizationStatus.Valid && res.Status != AuthorizationStatus.Invalid))
            {
                res = await authz.Resource();

                attempts--;

                // if status is not yet valid or invalid, wait a sec and try again
                if (res.Status != AuthorizationStatus.Valid && res.Status != AuthorizationStatus.Invalid)
                {
                    await Task.Delay(1000);
                }
            }

            if (res.Status == AuthorizationStatus.Valid)
            {
                pendingAuthorization.Identifier.IsAuthorizationPending = false;
                pendingAuthorization.Identifier.Status = "valid";
                pendingAuthorization.IsValidated = true;
            }
            else
            {
                pendingAuthorization.Identifier.Status = "invalid";

                //determine error
                try
                {
                    var challenge = res.Challenges.FirstOrDefault(c => c.Type == challengeType);
                    if (challenge != null)
                    {
                        var r = await _acme.HttpClient.Get<AcmeResponse<Challenge>>(challenge.Url);

                        pendingAuthorization.AuthorizationError = $"{r.Resource.Error.Detail} {r.Resource.Error.Status} {r.Resource.Error.Type}";
                    }
                }
                catch
                {
                    log.Warning("Failed to determine error message for failed authorization.");
                }
                pendingAuthorization.Identifier.ValidationError = "Failed";
                pendingAuthorization.Identifier.ValidationErrorType = "Error";
                pendingAuthorization.IsValidated = false;
            }
            return pendingAuthorization;
        }


        /// <summary>
        /// Once validation has completed for our requested domains we can complete the certificate
        /// request by submitting a Certificate Signing Request (CSR) to the CA
        /// </summary>
        /// <param name="log">  </param>
        /// <param name="primaryDnsIdentifier">  </param>
        /// <param name="alternativeDnsIdentifiers">  </param>
        /// <param name="config">  </param>
        /// <returns>  </returns>
        public async Task<ProcessStepResult> CompleteCertificateRequest(ILog log, CertRequestConfig config, string orderId, string pwd)
        {
            var orderContext = _currentOrders[orderId];

            // check order status, if it's not 'ready' then try a few more times before giving up
            var order = await orderContext.Resource();

            var attempts = 5;
            while (attempts > 0 && (order?.Status != OrderStatus.Ready && order?.Status != OrderStatus.Valid))
            {
                await Task.Delay(2000);
                order = await orderContext.Resource();
                attempts--;
            }

            if (order?.Status != OrderStatus.Ready && order?.Status != OrderStatus.Valid)
            {
                return new ProcessStepResult { IsSuccess = false, ErrorMessage = "Certificate Request did not complete. Order did not reach Ready status in the time allowed.", Result = order };
            }

            // generate temp keypair for signing CSR
            var keyAlg = KeyAlgorithm.RS256;

            if (!string.IsNullOrEmpty(config.CSRKeyAlg))
            {
                if (config.CSRKeyAlg == "RS256")
                {
                    keyAlg = KeyAlgorithm.RS256;
                }

                if (config.CSRKeyAlg == "ECDSA256")
                {
                    keyAlg = KeyAlgorithm.ES256;
                }

                if (config.CSRKeyAlg == "ECDSA384")
                {
                    keyAlg = KeyAlgorithm.ES384;
                }

                if (config.CSRKeyAlg == "ECDSA521")
                {
                    keyAlg = KeyAlgorithm.ES512;
                }
            }

            var csrKey = KeyFactory.NewKey(keyAlg);

            if (!string.IsNullOrEmpty(config.CustomPrivateKey))
            {
                csrKey = KeyFactory.FromPem(config.CustomPrivateKey);
            }

            var certFriendlyName = $"{config.PrimaryDomain} [Certify] ";

            // generate cert
            CertificateChain certificateChain = null;
            DateTime? certExpiration = null;
            try
            {
                if (order.Status == OrderStatus.Valid)
                {
                    // download existing cert
                    certificateChain = await orderContext.Download();
                }
                else
                {
                    if (!string.IsNullOrEmpty(config.CustomCSR))
                    {

                        // read custom CSR as pem, convert to bytes/der
                        var pemString = string.Join("",
                                config.CustomCSR
                                .Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(s => !s.Contains("BEGIN ") && !s.Contains("END ")).ToArray()
                                );

                        byte[] csrBytes = Convert.FromBase64String(pemString);

                        await orderContext.Finalize(csrBytes);

                        certificateChain = await orderContext.Download();
                    }
                    else
                    {
                        // finalise and download

                        certificateChain = await orderContext.Generate(new CsrInfo
                        {
                            CommonName = _idnMapping.GetAscii(config.PrimaryDomain)
                        }, csrKey);
                    }
                }

                var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateChain.Certificate.ToDer());
                certExpiration = cert.NotAfter;
                certFriendlyName += $"{ cert.GetEffectiveDateString()} to {cert.GetExpirationDateString()}";
            }
            catch (AcmeRequestException exp)
            {
                var msg = $"Failed to finalize certificate order:  {exp.Error?.Detail}";
                log.Error(msg);

                return new ProcessStepResult { ErrorMessage = msg, IsSuccess = false, Result = exp.Error };
            }

            // file will be named as {expiration yyyyMMdd}_{guid} e.g. 20290301_4fd1b2ea-7b6e-4dca-b5d9-e0e7254e568b
            var certId = certExpiration.Value.ToString("yyyyMMdd") + "_" + Guid.NewGuid().ToString().Substring(0, 8);

            var domainAsPath = config.PrimaryDomain.Replace("*", "_");

            // var pemPath = ExportFullCertPEM(null, certificateChain, certId, domainAsPath);

            var pfxPath = ExportFullCertPFX(certFriendlyName, pwd, csrKey, certificateChain, certId, domainAsPath);

            return new ProcessStepResult { IsSuccess = true, Result = pfxPath };
        }

        private byte[] GetCACertsFromStore(System.Security.Cryptography.X509Certificates.StoreName storeName)
        {
            // get list of known CAs as Issuer certs from cert store
            // derived from PR idea by @pkiguy https://github.com/webprofusion/certify/pull/340

            try
            {
                var store = new System.Security.Cryptography.X509Certificates.X509Store(
                    storeName,
                    System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine);

                store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);
                var allCACerts = store.Certificates;

                using (var writer = new StringWriter())
                {
                    var pemWriter = new PemWriter(writer);
                    var certParser = new X509CertificateParser();

                    var certAdded = false;
                    foreach (var c in allCACerts)
                    {
                        try
                        {
                            Org.BouncyCastle.X509.X509Certificate parsedCert = certParser.ReadCertificate(c.GetRawCertData());
                            pemWriter.WriteObject(parsedCert);
                            certAdded = true;
                        }
                        catch (Exception exp)
                        {
                            // failed to parse a cert
                            _log?.Error($"Failed to parse CA or intermediate cert: {c.FriendlyName} :: {exp}");
                        }
                    }

                    writer.Flush();

                    if (certAdded)
                    {
                        return System.Text.ASCIIEncoding.ASCII.GetBytes(writer.ToString());
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception exp)
            {
                _log?.Error($"CertesACMEProvider: failed to prepare CA issuer cache: {exp}");
                return null;
            }
        }

        /// <summary>
        /// Compile cache of root and intermediate CAs which may be in use to sign certs
        /// </summary>
        private void RefreshIssuerCertCache()
        {
            try
            {
                _issuerCertCache = new List<byte[]>();

                var rootCAs = GetCACertsFromStore(System.Security.Cryptography.X509Certificates.StoreName.Root);
                if (rootCAs != null)
                {
                    _issuerCertCache.Add(rootCAs);
                }

                var intermediates = GetCACertsFromStore(System.Security.Cryptography.X509Certificates.StoreName.CertificateAuthority);
                if (intermediates != null)
                {
                    _issuerCertCache.Add(intermediates);
                }
            }
            catch (Exception exp)
            {
                //TODO: log
                System.Diagnostics.Debug.WriteLine("Failed to properly cache issuer certs.");
            }
        }

        private string ExportFullCertPFX(string certFriendlyName, string pwd, IKey csrKey, CertificateChain certificateChain, string certId, string primaryDomainPath)
        {
            var storePath = Path.GetFullPath(Path.Combine(new string[] { _settingsFolder, "..", "assets", primaryDomainPath }));

            if (!System.IO.Directory.Exists(storePath))
            {
                System.IO.Directory.CreateDirectory(storePath);
            }

            var pfxFile = certId + ".pfx";
            var pfxPath = Path.Combine(storePath, pfxFile);

            var pfx = certificateChain.ToPfx(csrKey);

            if (_issuerCertCache.Any())
            {
                foreach (var c in _issuerCertCache)
                {
                    pfx.AddIssuers(c);
                }
            }

            byte[] pfxBytes;
            try
            {
                pfxBytes = pfx.Build(certFriendlyName, pwd);
                System.IO.File.WriteAllBytes(pfxPath, pfxBytes);
            }
            catch (Exception)
            {
                // if build failed, try refreshing issuer certs
                RefreshIssuerCertCache();

                try
                {
                    pfxBytes = pfx.Build(certFriendlyName, pwd);
                    System.IO.File.WriteAllBytes(pfxPath, pfxBytes);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to build certificate as PFX. Check system date/time is correct and that the issuing CA is a trusted root CA on this machine. :" + ex.Message);
                }
            }

            return pfxPath;
        }

        private string ExportFullCertPEM(IKey csrKey, CertificateChain certificateChain, string certId, string primaryDomainPath)
        {
            var storePath = Path.GetFullPath(Path.Combine(new string[] { _settingsFolder, "..", "assets", primaryDomainPath }));

            if (!System.IO.Directory.Exists(storePath))
            {
                System.IO.Directory.CreateDirectory(storePath);
            }

            if (!System.IO.Directory.Exists(storePath))
            {
                System.IO.Directory.CreateDirectory(storePath);
            }

            var pemPath = Path.Combine(storePath, certId + ".pem");

            // write pem in order of Private .key, primary server .crt, intermediate .crt, issuer.crt
            // note:
            // nginx needs combined primary + intermediate.crt as pem (ssl_certificate), plus .key (ssl_certificate_key)
            // apache needs combined primary.crt (SSLCertificateFile), intermediate.crt (SSLCertificateChainFile), plus private .key (SSLCertificateKeyFile)
            var pem = certificateChain.ToPem(csrKey);

            System.IO.File.WriteAllText(pemPath, pem);

            return pemPath;
        }

        public async Task<StatusMessage> RevokeCertificate(ILog log, ManagedCertificate managedCertificate)
        {
            // get current PFX, extract DER bytes
            try
            {
                var pkcs = new Org.BouncyCastle.Pkcs.Pkcs12Store(File.Open(managedCertificate.CertificatePath, FileMode.Open, FileAccess.Read), "".ToCharArray());

                var certAliases = pkcs.Aliases.GetEnumerator();
                certAliases.MoveNext();

                var certEntry = pkcs.GetCertificate(certAliases.Current.ToString());
                var certificate = certEntry.Certificate;

                // revoke certificate
                var der = certificate.GetEncoded();

                try
                {
                    await _acme.HttpClient.ConsumeNonce();
                }
                catch (Exception)
                {
                    return new StatusMessage
                    {
                        IsOK = false,
                        Message = "Failed to resume communication with Certificate Authority API. Try again later."
                    };
                }

                await _acme.RevokeCertificate(der, RevocationReason.Unspecified, null);
            }
            catch (Exception exp)
            {
                return new StatusMessage { IsOK = false, Message = $"Failed to revoke certificate: {exp.Message}" };
            }

            return new StatusMessage { IsOK = true, Message = "Certificate revoked" };
        }

        public List<RegistrationItem> GetContactRegistrations()
        {
            var list = new List<RegistrationItem>();
            if (IsAccountRegistered())
            {
                list.Add(new RegistrationItem { Name = _settings.AccountEmail });
            }
            return list;
        }

        public void EnableSensitiveFileEncryption()
        {
            //FIXME: not implemented
        }

        public Task<string> GetAcmeAccountStatus() => throw new NotImplementedException();
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Certify.Management;
using Certify.Models.Config;
using Certify.Models.Plugins;
using Certify.Models.Providers;
using Microsoft.ApplicationInsights.DataContracts;

namespace Certify.Core.Management.Challenges.DNS
{
    /// <summary>
    /// DNS Provider bridge to Posh-ACME DNS Scripts
    /// </summary>
    public class DnsProviderPoshACME : IDnsProvider
    {


        /*
            Implemented providers (Posh-ACME: https://github.com/rmbolger/Posh-ACME)
            [Akamai](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Akamai-Readme.md),
            [AutoDNS](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/AutoDNS-Readme.md),
            [ClouDNS](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/ClouDNS-Readme.md),
            [DNSPod](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DNSPod-Readme.md),
            [DNSimple](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DNSimple-Readme.md),
            [DomainOffensive](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DomainOffensive-Readme.md),
            [deSEC](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DeSEC-Readme.md),
            [DigitalOcean](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DOcean-Readme.md),
            [Dreamhost](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Dreamhost-Readme.md),
            [Dynu](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Dynu-Readme.md),
            [EasyDNS](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/EasyDNS-Readme.md),
            [Gandi](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Gandi-Readme.md),
            [Google Cloud](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/GCloud-Readme.md),
            [Hetzner](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Hetzner-Readme.md),
            [Hurricane Electric](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/HurricaneElectric-Readme.md),
            [Infoblox](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Infoblox-Readme.md),
            [IBM Cloud/SoftLayer](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/IBMSoftLayer-Readme.md),
            [Linode](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Linode-Readme.md),
            [Loopia](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Loopia-Readme.md),
            [LuaDns](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/LuaDns-Readme.md),
            [name.com](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/NameCom-Readme.md),
            [NS1](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/NS1-Readme.md),
            [PointDNS](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/PointDNS-Readme.md),
            [Rackspace](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Rackspace-Readme.md),
            [RFC2136](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/RFC2136-Readme.md),
            [Selectel](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Selectel-Readme.md),
            [UnoEuro](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/UnoEuro-Readme.md),
            [Yandex](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Yandex-Readme.md),
            [Zonomi](https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Zonomi-Readme.md)
        */

        public class PoshACMEDnsProviderProvider : IDnsProviderProviderPlugin
        {
            public IDnsProvider GetProvider(Type pluginType, string id)
            {
                foreach (var provider in ExtendedProviders)
                {
                    if (provider.Id == id)
                    {
                        var scriptPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"Scripts\DNS\PoshACME");
                        // TODO : move this out, shared config should be injected
                        var config = SharedUtils.ServiceConfigManager.GetAppServiceConfig();
                        return new DnsProviderPoshACME(scriptPath, config.PowershellExecutionPolicy) { DelegateProviderDefinition = provider };
                    }
                }
                return null;
            }

            public List<ChallengeProviderDefinition> GetProviders(Type pluginType)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ExtendedProviders.ToList();
                }
                else
                {
                    return new List<ChallengeProviderDefinition>();
                }
            }
        }


        private const int DefaultPropagationDelay = 90;

        private ILog _log;

        int IDnsProvider.PropagationDelaySeconds => (_customPropagationDelay != null ? (int)_customPropagationDelay : Definition.PropagationDelaySeconds);

        string IDnsProvider.ProviderId => Definition.Id;

        string IDnsProvider.ProviderTitle => Definition.Title;

        string IDnsProvider.ProviderDescription => Definition.Description;

        string IDnsProvider.ProviderHelpUrl => Definition.HelpUrl;

        public bool IsTestModeSupported => Definition.IsTestModeSupported;

        List<ProviderParameter> IDnsProvider.ProviderParameters => Definition.ProviderParameters;

        private int? _customPropagationDelay = null;

        private Dictionary<string, string> _parameters;
        private Dictionary<string, string> _credentials;

        private string _poshAcmeScriptPath = @"Scripts\DNS\PoshACME";
        private string _scriptExecutionPolicy = "Unrestricted";

        private static ProviderParameter _defaultPropagationDelayParam = new ProviderParameter
        {
            Key = "propagationdelay",
            Name = "Propagation Delay Seconds",
            IsRequired = false,
            IsPassword = false,
            Value = DefaultPropagationDelay.ToString(),
            IsCredential = false
        };

        /// <summary>
        /// if another provider uses this one as a base, consumer must set the delegate to override settings
        /// </summary>
        public ChallengeProviderDefinition DelegateProviderDefinition { get; set; }
        public static ChallengeProviderDefinition Definition => new ChallengeProviderDefinition
        {
            Id = "DNS01.Powershell",
            Title = "Powershell/PoshACME DNS",
            Description = "Validates DNS challenges via a user provided custom powershell script",
            HelpUrl = "http://docs.certifytheweb.com/",
            PropagationDelaySeconds = DefaultPropagationDelay,
            ProviderParameters = new List<ProviderParameter>{
                        new ProviderParameter{ Key="args",Name="Script arguments", IsRequired=false, IsPassword=false, Value=DefaultPropagationDelay.ToString(), IsCredential=false },
                       _defaultPropagationDelayParam
                    },
            ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
            Config = "Provider=Certify.Providers.DNS.Powershell",
            HandlerType = ChallengeHandlerType.POWERSHELL
        };

        /// <summary>
        /// List of definitions that use this provider as a base. Each defines the info, parameters, credentials and script to be run.
        /// </summary>

        public static List<ChallengeProviderDefinition> ExtendedProviders = new List<ChallengeProviderDefinition>
        {
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.DigitalOcean",
                Title = "DigitalOcean DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials (Personal Access Token)",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DOcean-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DOToken", Name = "API Token", IsRequired = true, Description = "Personal Access Token", IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=DOcean",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.GCloud",
                Title = "Google Cloud DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/GCloud-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "GCKeyFile", Name = "Key File Path", IsRequired = true, Description = "Full path to JSON account file", IsCredential = false },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=GCloud",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.AkamaiEdgeRC",
                Title = "Akamai DNS API with .edgerc file (using Posh-ACME)",
                Description = "Validates via DNS API using .edgerc file",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Akamai-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "AKUseEdgeRC", Name = "Use EdgeRC file", IsRequired = true, Description = "Set to true", Value="true", IsHidden=true, Type= OptionType.Boolean, IsCredential = false },
                    new ProviderParameter { Key = "AKEdgeRCFile", Name = "EdgeRC File Path", IsRequired = true, Description = "Full path to .edgerc", IsCredential = false },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Akamai",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Akamai",
                Title = "Akamai DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Akamai-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "AKHost", Name = "Host", IsRequired = true, Description = "e.g. myhost.akamaiapis.net", IsCredential = false },
                    new ProviderParameter { Key = "AKClientToken", Name = "Client Token", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "AKClientSecretInsecure", Name = "Client Secret", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "AKAccessToken", Name = "Access Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Akamai",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.AutoDNS",
                Title = "AutoDNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/AutoDNS-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "AutoDNSUser", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "AutoDNSPasswordInsecure", Name = "Password", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "AutoDNSContext", Name = "Context", IsRequired = true, IsCredential = false, Value="4" },
                    new ProviderParameter { Key = "AutoDNSGateway", Name = "Gateway Host", IsRequired = true, IsCredential = true, Value="gateway.autodns.com" },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=AutoDNS",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.ClouDNS",
                Title = "ClouDNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/ClouDNS-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "CDUserType", Name = "User Type", IsRequired = true, IsCredential = false, Value="auth-id",  OptionsList="auth-id;sub-auth-id;sub-auth-user;" , Type= OptionType.Select },
                    new ProviderParameter { Key = "CDUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "CDPasswordInsecure", Name = "Password", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=ClouDNS",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.DNSPod",
                Title = "DNSPod DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DNSPod-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DNSPodUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "DNSPodPwdInsecure", Name = "Password", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=DNSPod",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.DNSimple",
                Title = "DNSimple DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DNSimple-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DSTokenInsecure", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=DNSimple",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
             new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.DomainOffensive",
                Title = "DomainOffensive DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DomainOffensive-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DomOffTokenInsecure ", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=DomainOffensive",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.DeSEC",
                Title = "deSEC DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/DeSEC-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DSTokenInsecure", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=DeSEC",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Dreamhost",
                Title = "Dreamhost DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Dreamhost-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DreamhostApiKey", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Dreamhost",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Dynu",
                Title = "Dynu DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Dynu-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DynuClientID", Name = "Client ID", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "DynuSecret", Name = "Secret", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Dynu",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.EasyDNS",
                Title = "EasyDNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/EasyDNS-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "EDToken", Name = "Token", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "EDKey", Name = "Key", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "EDUseSandbox", Name = "Use EasyDNS Sandbox API", Type= OptionType.Boolean,  Value="false", IsCredential=false, IsHidden=false },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=EasyDNS",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {

                Id = "DNS01.API.PoshACME.Gandi",
                Title = "Gandi DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Gandi-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "GandiTokenInsecure", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Gandi",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
             new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Hetzner",
                Title = "Hetzner DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Hetzner-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "HetznerTokenInsecure", Name = "API Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Hetzner",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.HurricaneElectric",
                Title = "Hurricane Electric DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/HurricaneElectric-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "HEUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "HEPassword", Name = "Password", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=HurricaneElectric",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.IBMSoftLayer",
                Title = "IBM Cloud/SoftLayer DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/IBMSoftLayer-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "IBMUser", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "IBMKey", Name = "Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=IBMSoftLayer",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
             new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Infoblox",
                Title = "Infoblox DDI DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Infoblox-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "IBServer", Name = "Server", IsRequired = true, IsCredential = false, Description="e.g. gridmaster.example.com"  },
                    new ProviderParameter { Key = "IBUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "IBPassword", Name = "Password", IsRequired = true, IsCredential = true, IsPassword=true },
                    new ProviderParameter { Key = "IBView", Name = "DNS View", IsRequired = true, IsCredential = false, Description="e.g. default", Value="default"},
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Infoblox",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
             new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Linode",
                Title = "Linode DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Linode-Readme.md",
                PropagationDelaySeconds = 1020,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "LITokenInsecure", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Linode",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
              new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Loopia",
                Title = "Loopia DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Loopia-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "LoopiaUser", Name = "API Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "LoopiaPassInsecure", Name = "API User Password", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Loopia",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
                new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.LuaDns",
                Title = "LuaDns API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/LuaDns-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "LuaUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "LuaPassword", Name = "API Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=LuaDns",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.NameCom",
                Title = "name.com DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/NameCom-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "NameComUserName", Name = "APU Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "NameComToken", Name = "API Token", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "NameComUseTestEnv", Name = "Use Test Environment", IsRequired = true, Value="false", Type= OptionType.Boolean, IsHidden=true, IsCredential=false },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=NameCom",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.NS1",
                Title = "NS1 DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/NS1-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "NS1KeyInsecure", Name = "Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=NS1",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.PointDNS",
                Title = "PointDNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/PointDNS-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "PDUser", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "PDKeyInsecure", Name = "API Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=PointDNS",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Rackspace",
                Title = "Rackspace Cloud DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Rackspace-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "RSUsername", Name = "Username", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "RSApiKeyInsecure", Name = "API Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Rackspace",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.RFC2136",
                Title = "RFC2136 with nsupdate as DNS API (using Posh-ACME)",
                Description = "Validates via nsupdate, using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/RFC2136-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "DDNSNameserver", Name = "Nameserver", IsRequired = true, IsCredential = false, Description="e.g. ns.example.com" },
                    new ProviderParameter { Key = "DDNSExePath", Name = "Path to nsupdate exe", IsRequired = true, IsCredential = false, Description="e.g. C:\\BIND\\nsupdate.exe" },
                    new ProviderParameter { Key = "DDNSPort", Name = "DDNS Port", IsRequired = false, IsCredential = false, Description="e.g. 53 (optional)" },
                    new ProviderParameter { Key = "DDNSKeyType", Name = "Key Type", IsRequired = true, IsCredential = false, Value="hmac-sha256", OptionsList="hmac-md5;hmac-sha1;hmac-sha224;hmac-sha256;hmac-sha384;hmac-sha512"},
                    new ProviderParameter { Key = "DDNSKeyName", Name = "Key Name", IsRequired = true, IsCredential = true, Description="e.g. mykey" },
                    new ProviderParameter { Key = "DDNSKeyValueInsecure", Name = "DDNS Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=RFC2136",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Selectel",
                Title = "Selectel DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Selectel-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "SelectelAdminTokenInsecure", Name = "API Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Selectel",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.UnoEuro",
                Title = "UnoEuro DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/UnoEuro-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "UEAccount", Name = "Account", IsRequired = true, IsCredential = true },
                    new ProviderParameter { Key = "UEAPIKey", Name = "API Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=UnoEuro",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Yandex",
                Title = "Yandex DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Yandex-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "YDAdminTokenInsecure", Name = "Token", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Yandex",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            },
            new ChallengeProviderDefinition
            {
                Id = "DNS01.API.PoshACME.Zonomi",
                Title = "Zonomi DNS API (using Posh-ACME)",
                Description = "Validates via DNS API using credentials",
                HelpUrl = "https://github.com/rmbolger/Posh-ACME/blob/master/Posh-ACME/DnsPlugins/Zonomi-Readme.md",
                PropagationDelaySeconds = DefaultPropagationDelay,
                ProviderParameters = new List<ProviderParameter>
                {
                    new ProviderParameter { Key = "ZonomiApiKey", Name = "API Key", IsRequired = true, IsCredential = true },
                    _defaultPropagationDelayParam
                },
                ChallengeType = Models.SupportedChallengeTypes.CHALLENGE_TYPE_DNS,
                Config = "Provider=Certify.Providers.DNS.PoshACME;Script=Zonomi",
                HandlerType = ChallengeHandlerType.POWERSHELL,
                IsTestModeSupported = true,
                IsExperimental = true
            }
        };

        public DnsProviderPoshACME(string scriptPath, string scriptExecutionPolicy)
        {
            _scriptExecutionPolicy = scriptExecutionPolicy;

            if (scriptPath != null)
            {
                _poshAcmeScriptPath = scriptPath;
            }
        }

        private string PrepareScript(string action, string recordName, string recordValue)
        {
            var config = DelegateProviderDefinition.Config.Split(';');

            var script = config.FirstOrDefault(c => c.StartsWith("Script="))?.Split('=')[1];

            var scriptFile = Path.Combine(_poshAcmeScriptPath, "DnsPlugins", script);

            var wrapper = $" $PoshACMERoot = \"{_poshAcmeScriptPath}\" \r\n";
            wrapper += File.ReadAllText(Path.Combine(_poshAcmeScriptPath, "Posh-ACME-Wrapper.ps1"));

            var scriptContent = wrapper + "\r\n. \"" + scriptFile + ".ps1\" \r\n";

            // arrange params and credentials into one ordered set of arguments
            var set = DelegateProviderDefinition
                .ProviderParameters
                .Where(p => p.Key != "propagationdelay")
                .Select(s => _parameters.Keys.Contains(s.Key) ? _parameters.FirstOrDefault(p => p.Key == s.Key) : _credentials.FirstOrDefault(c => c.Key == s.Key));

            var args = string.Join("; ",
                            set.Select(p =>
                                DelegateProviderDefinition
                                .ProviderParameters
                                .FirstOrDefault(a => a.Key == p.Key)?.Type == OptionType.Boolean ?
                                    p.Key + "=" + (bool.Parse(p.Value) == true ? "$true" : "$false") : // bool param
                                    p.Key + "='" + p.Value + "'" // string param
                            )
                        );

            scriptContent += " $PluginArgs= @{" + args + "} \r\n";
            scriptContent += $"{action}{script} -RecordName '{recordName}' -TxtValue '{recordValue}' @PluginArgs \r\n";

            return scriptContent;
        }

        public async Task<ActionResult> CreateRecord(DnsRecord request)
        {
            var scriptContent = PrepareScript("Add-DnsTxt", request.RecordName, request.RecordValue);

            var objParams = _parameters.ToDictionary(p => p.Key, p => p.Value as object);

            return await PowerShellManager.RunScript(_scriptExecutionPolicy, null, null, objParams, scriptContent, null);
        }

        public async Task<ActionResult> DeleteRecord(DnsRecord request)
        {
            var scriptContent = PrepareScript("Remove-DnsTxt", request.RecordName, request.RecordValue);

            var objParams = _parameters.ToDictionary(p => p.Key, p => p.Value as object);

            return await PowerShellManager.RunScript(_scriptExecutionPolicy, null, null, objParams, scriptContent, null);
        }

        Task<List<DnsZone>> IDnsProvider.GetZones() => Task.FromResult(new List<DnsZone>());

        Task<bool> IDnsProvider.InitProvider(Dictionary<string, string> credentials, Dictionary<string, string> parameters, ILog log)
        {
            _log = log;

            _credentials = credentials;
            _parameters = parameters;

            if (parameters?.ContainsKey("propagationdelay") == true)
            {
                if (int.TryParse(parameters["propagationdelay"], out int customPropDelay))
                {
                    _customPropagationDelay = customPropDelay;
                }
            }

            return Task.FromResult(true);
        }

        Task<ActionResult> IDnsProvider.Test() => Task.FromResult(new ActionResult
        {
            IsSuccess = true,
            Message = "Test skipped for DNS updates via Posh-ACME. No test available."
        });
    }
}

﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Certify.Config;
using Certify.Config.Migration;
using Certify.Models;
using Certify.Models.Config;
using Certify.Models.Providers;
using Certify.Providers;

namespace Certify.Management
{
    public interface ICertifyManager
    {
        void SetStatusReporting(IStatusReporting statusReporting);

        Task<bool> IsServerTypeAvailable(StandardServerTypes serverType);

        Task<Version> GetServerTypeVersion(StandardServerTypes serverType);

        Task<List<ActionStep>> RunServerDiagnostics(StandardServerTypes serverType, string siteId);

        Task<ManagedCertificate> GetManagedCertificate(string id);

        Task<List<ManagedCertificate>> GetManagedCertificates(ManagedCertificateFilter filter = null);

        Task<ManagedCertificate> UpdateManagedCertificate(ManagedCertificate site);

        Task DeleteManagedCertificate(string id);

        Task<ImportExportPackage> PerformExport(ExportRequest exportRequest);
        Task<List<ActionStep>> PerformImport(ImportRequest importRequest);

        Task<List<SimpleAuthorizationChallengeItem>> GetCurrentChallengeResponses(string challengeType, string key = null);

        Task<List<AccountDetails>> GetAccountRegistrations();

        Task<ActionResult> AddAccount(ContactRegistration reg);

        Task<ActionResult> RemoveAccount(string storageKey);

        Task<List<StatusMessage>> TestChallenge(ILog log, ManagedCertificate managedCertificate, bool isPreviewMode, IProgress<RequestProgressState> progress = null);

        Task<List<DnsZone>> GetDnsProviderZones(string providerTypeId, string credentialsId);
        Task<ActionResult> UpdateCertificateAuthority(CertificateAuthority certificateAuthority);
        Task<List<CertificateAuthority>> GetCertificateAuthorities();
  
        Task<StatusMessage> RevokeCertificate(ILog log, ManagedCertificate managedCertificate);

        Task<CertificateRequestResult> PerformDummyCertificateRequest(ManagedCertificate managedCertificate, IProgress<RequestProgressState> progress = null);
        Task<ActionResult> RemoveCertificateAuthority(string id);
        Task<List<BindingInfo>> GetPrimaryWebSites(bool ignoreStoppedSites);

        void BeginTrackingProgress(RequestProgressState state);

        Task<CertificateRequestResult> DeployCertificate(ManagedCertificate managedCertificate, IProgress<RequestProgressState> progress = null, bool isPreviewOnly = false);

        Task<CertificateRequestResult> FetchCertificate(ManagedCertificate managedCertificate, IProgress<RequestProgressState> progress = null, bool isPreviewOnly = false);

        Task<CertificateRequestResult> PerformCertificateRequest(ILog log, ManagedCertificate managedCertificate, IProgress<RequestProgressState> progress = null, bool resumePaused = false, bool skipRequest = false, bool failOnSkip = false);

        Task<List<DomainOption>> GetDomainOptionsFromSite(string siteId);

        Task<List<CertificateRequestResult>> PerformRenewalAllManagedCertificates(RenewalSettings settings, Dictionary<string, Progress<RequestProgressState>> progressTrackers = null);

        RequestProgressState GetRequestProgressState(string managedItemId);

        Task<bool> PerformPeriodicTasks();

        Task<bool> PerformDailyTasks();

        Task PerformCertificateCleanup();

        Task<List<ActionStep>> GeneratePreview(ManagedCertificate item);

        void ReportProgress(IProgress<RequestProgressState> progress, RequestProgressState state, bool logThisEvent = true);

        Task<List<ActionStep>> PerformDeploymentTask(ILog log, string managedCertificateId, string taskId, bool isPreviewOnly, bool skipDeferredTasks, bool forceTaskExecution);

        Task<List<DeploymentProviderDefinition>> GetDeploymentProviders();

        Task<List<ActionResult>> ValidateDeploymentTask(ManagedCertificate managedCertificate, DeploymentTaskConfig taskConfig);

        Task<DeploymentProviderDefinition> GetDeploymentProviderDefinition(string id, DeploymentTaskConfig config);
    }
}

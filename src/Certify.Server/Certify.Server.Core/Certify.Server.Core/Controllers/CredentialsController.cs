﻿using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Certify.Management;
using Certify.Models.Config;
using Microsoft.AspNetCore.Mvc;

namespace Certify.Service
{
    [ApiController]
    [Route("api/credentials")]
    public class CredentialsController : Controllers.ControllerBase
    {
        private CredentialsManager credentialsManager = new CredentialsManager(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        [HttpGet, Route("")]
        public async Task<List<StoredCredential>> GetCredentials()
        {
            return await credentialsManager.GetCredentials();
        }

        [HttpPost, Route("")]
        public async Task<StoredCredential> UpdateCredentials(StoredCredential credential)
        {
            DebugLog();

            return await credentialsManager.Update(credential);
        }

        [HttpDelete, Route("{storageKey}")]
        public async Task<bool> DeleteCredential(string storageKey)
        {
            DebugLog();

            return await credentialsManager.Delete(storageKey);
        }

        [HttpPost, Route("{storageKey}/test")]
        public async Task<Models.Config.ActionResult> TestCredentials(string storageKey)
        {
            DebugLog();

            return await credentialsManager.TestCredentials(storageKey);
        }
    }
}

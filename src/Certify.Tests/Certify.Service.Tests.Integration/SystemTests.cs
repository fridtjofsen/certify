﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Certify.Service.Tests.Integration
{
    [TestClass]
    public class SystemTests: ServiceTestBase
    {
        [TestMethod]
        public async Task TestVersionCheck()
        {
            var result = await _client.GetAppVersion();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task TestUpdateCheck()
        {
            var result = await _client.CheckForUpdates();

            Assert.IsNotNull(result.Version);
        }
    }
}

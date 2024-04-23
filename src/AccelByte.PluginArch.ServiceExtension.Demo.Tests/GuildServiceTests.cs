// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System.Collections.Generic;
using System.Threading.Tasks;

using NUnit.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AccelByte.PluginArch.ServiceExtension.Demo.Server.Services;

using AccelByte.Sdk.Core;
using AccelByte.PluginArch.ServiceExtension.Demo.Server.Model;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Tests
{
    [TestFixture]
    public class GuildServiceTests
    {
        private ILogger<SampleGuildService> _ServiceLogger;

        public GuildServiceTests()
        {
            ILoggerFactory loggerFactory = new NullLoggerFactory();
            _ServiceLogger = loggerFactory.CreateLogger<SampleGuildService>();
        }

        [Test]
        public async Task GuildServiceTest()
        {
            using AccelByteSDK adminSdk = AccelByteSDK.Builder
                .UseDefaultHttpClient()
                .UseDefaultConfigRepository()
                .UseDefaultTokenRepository()
                .Build();
            adminSdk.LoginClient();

            var service = new SampleGuildService(_ServiceLogger, new TestAccelByteServiceProvider(adminSdk));
            var callContext = new UnitTestCallContext();

            GuildProgressData data = new GuildProgressData()
            {
                Namespace = adminSdk.Namespace,
                Objectives = new Dictionary<string, int>()
                {
                    {"objective_1", 2 },
                    {"objective_2", 3 }
                }
            };

            var createResponse = await service.CreateOrUpdateGuildProgress(new Extend.ServiceExtension.CreateOrUpdateGuildProgressRequest()
            {
                Namespace = adminSdk.Namespace,
                GuildProgress = data.ToGuildProgressGrpcData()
            }, callContext);

            Assert.IsNotNull(createResponse);
            Assert.AreEqual(adminSdk.Namespace, createResponse.GuildProgress.Namespace);

            GuildProgressData progressData = GuildProgressData.FromGuildProgressGrpcData(createResponse.GuildProgress);
            Assert.IsTrue(progressData.Objectives.ContainsKey("objective_1"));
            Assert.AreEqual(2, progressData.Objectives["objective_1"]);

            string guildId = progressData.GuildId;


            var getResponse = await service.GetGuildProgress(new Extend.ServiceExtension.GetGuildProgressRequest()
            {
                Namespace = adminSdk.Namespace,
                GuildId = guildId
            }, callContext);
            Assert.IsNotNull(getResponse);

            GuildProgressData getProgressData = GuildProgressData.FromGuildProgressGrpcData(getResponse.GuildProgress);
            Assert.IsTrue(getProgressData.Objectives.ContainsKey("objective_1"));
            Assert.AreEqual(2, getProgressData.Objectives["objective_1"]);
        }
    }
}

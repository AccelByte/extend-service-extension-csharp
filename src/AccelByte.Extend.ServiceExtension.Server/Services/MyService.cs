// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using AccelByte.Sdk.Api;
using AccelByte.Extend.ServiceExtension.Server.Model;

namespace AccelByte.Extend.ServiceExtension.Server.Services
{
    public class MyService : Service.ServiceBase
    {
        private readonly ILogger<MyService> _Logger;

        private readonly IAccelByteServiceProvider _ABProvider;

        public MyService(
            ILogger<MyService> logger,
            IAccelByteServiceProvider abProvider)
        {
            _Logger = logger;
            _ABProvider = abProvider;
        }

        public override Task<CreateOrUpdateGuildProgressResponse> CreateOrUpdateGuildProgress(CreateOrUpdateGuildProgressRequest request, ServerCallContext context)
        {
            string actualGuildId = request.GuildProgress.GuildId.Trim();
            if (actualGuildId == "")
                actualGuildId = Guid.NewGuid().ToString().Replace("-", "");

            string gpKey = $"guildProgress_{actualGuildId}";
            var gpValue = GuildProgressData.FromGuildProgressGrpcData(request.GuildProgress);
            gpValue.GuildId = actualGuildId;

            var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminPostGameRecordHandlerV1Op
                .Execute<GuildProgressData>(gpValue, gpKey, request.Namespace);
            if (response == null)
                throw new Exception("NULL response from cloudsave service.");

            GuildProgressData savedData = response.Value!;

            return Task.FromResult(new CreateOrUpdateGuildProgressResponse()
            {
                GuildProgress = savedData.ToGuildProgressGrpcData()
            });
        }

        public override Task<GetGuildProgressResponse> GetGuildProgress(GetGuildProgressRequest request, ServerCallContext context)
        {
            string gpKey = $"guildProgress_{request.GuildId.Trim()}";

            var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminGetGameRecordHandlerV1Op
                .Execute<GuildProgressData>(gpKey, request.Namespace);
            if (response == null)
                throw new Exception("NULL response from cloudsave service.");

            GuildProgressData savedData = response.Value!;
            return Task.FromResult(new GetGuildProgressResponse()
            {
                GuildProgress = savedData.ToGuildProgressGrpcData()
            });
        }
    }
}

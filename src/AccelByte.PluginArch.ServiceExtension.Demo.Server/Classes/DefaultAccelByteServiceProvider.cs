// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Feature.LocalTokenValidation;
using AccelByte.Sdk.Feature.AutoTokenRefresh;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class DefaultAccelByteServiceProvider : IAccelByteServiceProvider
    {
        private ILogger<DefaultAccelByteServiceProvider> _Logger;

        public AccelByteSDK Sdk { get; }

        public AppSettingConfigRepository Config { get; }

        public DefaultAccelByteServiceProvider(IConfiguration config, ILogger<DefaultAccelByteServiceProvider> logger)
        {
            _Logger = logger;
            AppSettingConfigRepository? abConfig = config.GetSection("AccelByte").Get<AppSettingConfigRepository>();
            if (abConfig == null)
                throw new Exception("Missing AccelByte configuration section.");
            abConfig.ReadEnvironmentVariables();
            Config = abConfig;

            bool enableAuthorization = config.GetValue<bool>("EnableAuthorization");
            string? strEnableAuth = Environment.GetEnvironmentVariable("PLUGIN_GRPC_SERVER_AUTH_ENABLED");
            if ((strEnableAuth != null) && (strEnableAuth != String.Empty))
                enableAuthorization = (strEnableAuth.Trim().ToLower() == "true");

            if (enableAuthorization)
            {
                Sdk = AccelByteSDK.Builder
                    .SetConfigRepository(Config)
                    .UseDefaultCredentialRepository()
                    .UseDefaultHttpClient()
                    .UseDefaultTokenRepository()
                    .UseLocalTokenValidator()
                    .UseAutoRefreshForTokenRevocationList()
                    .UseAutoTokenRefresh()
                    .Build();
            }
            else
            {
                Sdk = AccelByteSDK.Builder
                    .SetConfigRepository(Config)
                    .UseDefaultCredentialRepository()
                    .UseDefaultHttpClient()
                    .UseDefaultTokenRepository()
                    .UseAutoTokenRefresh()
                    .Build();
            }

            Sdk.LoginClient();
        }
    }
}

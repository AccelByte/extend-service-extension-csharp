// Copyright (c) 2023-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

using AccelByte.Sdk.Core.Logging;
using AccelByte.Sdk.Core.Repository;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public class AppSettingConfigRepository : IConfigRepository
    {
        public string BaseUrl { get; set; } = String.Empty;

        public string ClientId { get; set; } = String.Empty;

        public string ClientSecret { get; set; } = String.Empty;

        public string AppName { get; set; } = String.Empty;

        public string TraceIdVersion { get; set; } = String.Empty;

        public string Namespace { get; set; } = String.Empty;

        public bool EnableTraceId { get; set; } = false;

        public bool EnableUserAgentInfo { get; set; } = false;

        public string ResourceName { get; set; } = "";

        public string ServiceName { get; set; } = "";

        public IHttpLogger? Logger { get; set; } = null;

        public void ReadEnvironmentVariables()
        {
            string? abBaseUrl = Environment.GetEnvironmentVariable("AB_BASE_URL");
            if ((abBaseUrl != null) && (abBaseUrl.Trim() != String.Empty))
                BaseUrl = abBaseUrl.Trim();

            string? abClientId = Environment.GetEnvironmentVariable("AB_CLIENT_ID");
            if ((abClientId != null) && (abClientId.Trim() != String.Empty))
                ClientId = abClientId.Trim();

            string? abClientSecret = Environment.GetEnvironmentVariable("AB_CLIENT_SECRET");
            if ((abClientSecret != null) && (abClientSecret.Trim() != String.Empty))
                ClientSecret = abClientSecret.Trim();

            string? abNamespace = Environment.GetEnvironmentVariable("AB_NAMESPACE");
            if ((abNamespace != null) && (abNamespace.Trim() != String.Empty))
                Namespace = abNamespace.Trim();

            string? appServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME");
            if (appServiceName == null)
                ServiceName = "extend-app-service-extension";
            else
                ServiceName = $"extend-app-se-{appServiceName.Trim().ToLower()}";

            string? appResourceName = Environment.GetEnvironmentVariable("APP_RESOURCE_NAME");
            if (appResourceName == null)
                appResourceName = "SERVICEEXTENSIONEXTENDAPP";
            ResourceName = appResourceName;
        }
    }
}

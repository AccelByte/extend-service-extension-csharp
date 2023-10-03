// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Api;
using AccelByte.Sdk.Feature.LocalTokenValidation;
using AccelByte.Sdk.Feature.AutoTokenRefresh;
using System.Collections.Generic;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class DefaultAccelByteServiceProvider : TokenValidator, IAccelByteServiceProvider
    {
        private ILogger<DefaultAccelByteServiceProvider> _Logger;

        private Dictionary<string, List<LocalPermissionItem>> _PermissionCache = new();

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

            Sdk = AccelByteSDK.Builder
                .SetConfigRepository(Config)
                .UseDefaultCredentialRepository()
                .UseDefaultHttpClient()
                .UseDefaultTokenRepository()
                .UseAutoTokenRefresh()
                .Build();

            Sdk.LoginClient();
        }

        public List<LocalPermissionItem> GetRolePermission(string roleId)
        {
            if (_PermissionCache.ContainsKey(roleId))
                return _PermissionCache[roleId];

            try
            {
                var response = Sdk.Iam.Roles.AdminGetRoleV4Op.Execute(roleId);
                if (response == null)
                    throw new Exception("Null response");

                List<LocalPermissionItem> permissions = new List<LocalPermissionItem>();
                foreach (var item in response.Permissions!)
                {
                    permissions.Add(new LocalPermissionItem()
                    {
                        Resource = item.Resource!,
                        Action = item.Action!.Value
                    });
                }

                _PermissionCache[roleId] = permissions;
                return permissions;
            }
            catch (Exception x)
            {
                _Logger.LogError($"Could not fetch role data for id {roleId}. {x.Message}");
                return new List<LocalPermissionItem>();
            }
        }

        public bool ValidatePermission(AccessTokenPayload payload, string permission, int action)
        {
            try
            {
                bool foundMatchingPermission = false;

                if ((payload.Permissions != null) && (payload.Permissions.Count > 0))
                {
                    foreach (var p in payload.Permissions)
                    {
                        if (IsResourceAllowed(p.Resource, permission))
                        {
                            if (PermissionAction.Has(p.Action, action))
                            {
                                foundMatchingPermission = true;
                                break;
                            }
                        }
                    }
                }
                else if ((payload.NamespaceRoles != null) && (payload.NamespaceRoles.Count > 0))
                {
                    foreach (var r in payload.NamespaceRoles)
                    {
                        var permissions = GetRolePermission(r.RoleId);
                        foreach (var p in permissions)
                        {
                            if (IsResourceAllowed(p.Resource, permission))
                            {
                                if (PermissionAction.Has(p.Action, action))
                                {
                                    foundMatchingPermission = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                return foundMatchingPermission;
            }
            catch
            {
                return false;
            }
        }
    }
}

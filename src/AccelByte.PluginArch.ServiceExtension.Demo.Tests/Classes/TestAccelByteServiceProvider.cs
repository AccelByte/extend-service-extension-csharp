// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Api;
using AccelByte.Sdk.Feature.LocalTokenValidation;
using AccelByte.Sdk.Feature.AutoTokenRefresh;
using AccelByte.PluginArch.ServiceExtension.Demo.Server;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Tests
{
    public class TestAccelByteServiceProvider : TokenValidator, IAccelByteServiceProvider
    {
        private Dictionary<string, List<LocalPermissionItem>> _PermissionCache = new();

        public AccelByteSDK Sdk { get; }

        public AppSettingConfigRepository Config { get; }
        

        public TestAccelByteServiceProvider(AccelByteSDK sdk)
        {
            Sdk = sdk;
            Config = new AppSettingConfigRepository();
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
            catch
            {
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

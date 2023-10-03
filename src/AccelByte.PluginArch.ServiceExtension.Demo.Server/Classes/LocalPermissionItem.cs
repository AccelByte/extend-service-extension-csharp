// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;

using AccelByte.Sdk.Core.Logging;
using AccelByte.Sdk.Core.Repository;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class LocalPermissionItem
    {
        public string Resource { get; set; } = String.Empty;

        public int Action { get; set; } = 0;
    }
}
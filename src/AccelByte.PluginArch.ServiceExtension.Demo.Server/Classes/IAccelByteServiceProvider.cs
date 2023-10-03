// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using AccelByte.Sdk.Core;
using AccelByte.Sdk.Feature.LocalTokenValidation;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public interface IAccelByteServiceProvider
    {
        AccelByteSDK Sdk { get; }

        AppSettingConfigRepository Config { get; }

        bool ValidatePermission(AccessTokenPayload payload, string permission, int action);
    }
}

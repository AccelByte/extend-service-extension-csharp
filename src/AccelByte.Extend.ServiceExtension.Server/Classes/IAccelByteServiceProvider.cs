// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Sdk.Core;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public interface IAccelByteServiceProvider
    {
        AccelByteSDK Sdk { get; }

        AppSettingConfigRepository Config { get; }
    }
}

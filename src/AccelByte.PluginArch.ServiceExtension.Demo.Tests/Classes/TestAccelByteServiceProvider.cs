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
        public AccelByteSDK Sdk { get; }

        public AppSettingConfigRepository Config { get; }
        

        public TestAccelByteServiceProvider(AccelByteSDK sdk)
        {
            Sdk = sdk;
            Config = new AppSettingConfigRepository();
        }        
    }
}

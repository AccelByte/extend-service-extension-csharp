// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Sdk.Core;

namespace AccelByte.Extend.ServiceExtension.Server.Tests
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

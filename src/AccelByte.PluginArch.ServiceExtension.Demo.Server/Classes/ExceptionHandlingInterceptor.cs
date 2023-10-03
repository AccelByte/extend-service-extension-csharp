// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class ExceptionHandlingInterceptor : Interceptor
    {

    }
}

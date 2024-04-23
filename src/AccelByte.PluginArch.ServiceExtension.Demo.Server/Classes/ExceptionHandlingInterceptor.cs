// Copyright (c) 2022-2024 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class ExceptionHandlingInterceptor : Interceptor
    {
        private readonly ILogger<ExceptionHandlingInterceptor> _Logger;

        public ExceptionHandlingInterceptor(ILogger<ExceptionHandlingInterceptor> logger)
        {
            _Logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (Exception x)
            {
                _Logger.LogError($"{context.Method} - Error: {x.Message}");
                throw new RpcException(new Status(StatusCode.Internal, x.Message));
            }
        }
    }
}
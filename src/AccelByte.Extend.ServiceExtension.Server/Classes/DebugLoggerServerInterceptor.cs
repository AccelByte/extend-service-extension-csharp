// Copyright (c) 2022 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public class DebugLoggerServerInterceptor : Interceptor
    {
        private readonly ILogger _Logger;

        public DebugLoggerServerInterceptor(ILogger<DebugLoggerServerInterceptor> logger, IConfiguration appConfig)
        {
            _Logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            _Logger.LogInformation($"REQUEST {context.Method}");
            _Logger.LogInformation($"REQUEST {context.RequestHeaders.ToString()}");

            try
            {
                var result = await continuation(request, context);

                _Logger.LogInformation($"RESPONSE {context.Method}");
                _Logger.LogInformation($"RESPONSE {context.ResponseTrailers.ToString()}");

                return result;
            }
            catch (Exception x)
            {
                _Logger.LogError(x, $"Error thrown by {context.Method}.");
                throw;
            }
        }
    }
}

// Copyright (c) 2022-2024 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Google.Protobuf.Reflection;

using AccelByte.Extend.ServiceExtension;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server
{
    public class AuthorizationInterceptor : Interceptor
    {
        private readonly ILogger<AuthorizationInterceptor> _Logger;

        private readonly IAccelByteServiceProvider _ABProvider;

        public AuthorizationInterceptor(ILogger<AuthorizationInterceptor> logger, IAccelByteServiceProvider abSdkProvider)
        {
            _Logger = logger;
            _ABProvider = abSdkProvider;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            string methodName = context.Method.Replace('/', '.').Substring(1);
            MethodDescriptor? methodDesc = null;
            foreach (var mdItem in Service.Descriptor.Methods)
            {
                if (mdItem.FullName == methodName)
                {
                    methodDesc = mdItem;
                    break;
                }
            }

            if (methodDesc == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Suitable method not found."));

            MethodOptions mOpts = methodDesc.GetOptions();

            string qPermission = "";
            if (mOpts.HasExtension(PermissionExtensions.Resource))
                qPermission = mOpts.GetExtension(PermissionExtensions.Resource);

            Extend.ServiceExtension.Action qAction = 0;
            if (mOpts.HasExtension(PermissionExtensions.Action))
                qAction = mOpts.GetExtension(PermissionExtensions.Action);

            try
            {
                string? authToken = context.RequestHeaders.GetValue("authorization");
                if (authToken == null)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "No authorization token provided."));

                string[] authParts = authToken.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (authParts.Length != 2)
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid authorization token format"));

                int actNum = (int)qAction;
                bool b = _ABProvider.Sdk.ValidateToken(authParts[1], qPermission, actNum);
                if (!b)
                    throw new RpcException(new Status(StatusCode.PermissionDenied, $"Permission {qPermission} [{qAction}] is required."));                
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception x)
            {
                _Logger.LogError(x, $"Authorization error: {x.Message}");
                throw new RpcException(new Status(StatusCode.Unauthenticated, x.Message));
            }

            return await continuation(request, context);
        }
    }
}

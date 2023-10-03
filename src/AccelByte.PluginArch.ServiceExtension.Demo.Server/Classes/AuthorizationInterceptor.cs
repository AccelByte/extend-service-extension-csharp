﻿// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Google.Protobuf.Reflection;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Feature.LocalTokenValidation;
using AccelByte.Custom.Guild;

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
            foreach (var mdItem in GuildService.Descriptor.Methods)
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

            Custom.Guild.Action qAction = 0;
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

                AccessTokenPayload? tokenPayload = _ABProvider.Sdk.ParseAccessToken(authParts[1], true);
                if (tokenPayload == null)
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authorization token."));
                
                int actNum = (int)qAction;
                bool b = _ABProvider.ValidatePermission(tokenPayload, qPermission, actNum);
                if (!b)
                    throw new RpcException(new Status(StatusCode.PermissionDenied, $"Permission {qPermission} [{qAction}] is required."));

                return await continuation(request, context);
            }
            catch (Exception x)
            {
                _Logger.LogError(x, $"Authorization error: {x.Message}");
                throw new RpcException(new Status(StatusCode.Unauthenticated, x.Message));
            }
        }
    }
}

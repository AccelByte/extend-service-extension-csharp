// Copyright (c) 2022-2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using Grpc.Core.Interceptors;
using Google.Protobuf.Reflection;

using AccelByte.Sdk.Core;
using AccelByte.Sdk.Feature.LocalTokenValidation;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public class AuthorizationInterceptor : Interceptor
    {
        private static readonly Regex NamespacePlaceholderRegex = 
            new Regex(@"\{(namespace|NAMESPACE)\}", RegexOptions.Compiled);

        // Cached extension class types (runtime discovery, no compile-time dependencies)
        private static readonly Lazy<Type?> CachedPermissionExtensionsType = 
            new Lazy<Type?>(() => FindExtensionClass("PermissionExtensions"));
        private static readonly Lazy<Type?> CachedAnnotationsExtensionsType = 
            new Lazy<Type?>(() => FindExtensionClass("AnnotationsExtensions"));

        // Cached PropertyInfo for extension field properties
        private static readonly Lazy<PropertyInfo?> CachedPermissionResourceProp = 
            new Lazy<PropertyInfo?>(() => CachedPermissionExtensionsType.Value?.GetProperty("Resource", BindingFlags.Public | BindingFlags.Static));
        private static readonly Lazy<PropertyInfo?> CachedPermissionActionProp = 
            new Lazy<PropertyInfo?>(() => CachedPermissionExtensionsType.Value?.GetProperty("Action", BindingFlags.Public | BindingFlags.Static));
        private static readonly Lazy<PropertyInfo?> CachedOpenapiv2OperationProp = 
            new Lazy<PropertyInfo?>(() => CachedAnnotationsExtensionsType.Value?.GetProperty("Openapiv2Operation", BindingFlags.Public | BindingFlags.Static));

        // Cached extension field values (used as parameters to HasExtension/GetExtension)
        private static readonly Lazy<object?> CachedPermissionResourceExtension = 
            new Lazy<object?>(() => CachedPermissionResourceProp.Value?.GetValue(null));
        private static readonly Lazy<object?> CachedPermissionActionExtension = 
            new Lazy<object?>(() => CachedPermissionActionProp.Value?.GetValue(null));
        private static readonly Lazy<object?> CachedOpenapiv2OperationExtension = 
            new Lazy<object?>(() => CachedOpenapiv2OperationProp.Value?.GetValue(null));

        // Cached MethodInfo objects (eliminates GetMethod() calls on every request)
        private static readonly Lazy<MethodInfo?> CachedHasExtensionForResource =
            new Lazy<MethodInfo?>(() => CachedPermissionResourceExtension.Value != null
                ? typeof(MethodOptions).GetMethod("HasExtension", new[] { CachedPermissionResourceExtension.Value.GetType() })
                : null);
        private static readonly Lazy<MethodInfo?> CachedGetExtensionForResource =
            new Lazy<MethodInfo?>(() => CachedPermissionResourceExtension.Value != null
                ? typeof(MethodOptions).GetMethod("GetExtension", new[] { CachedPermissionResourceExtension.Value.GetType() })
                : null);
        private static readonly Lazy<MethodInfo?> CachedHasExtensionForAction =
            new Lazy<MethodInfo?>(() => CachedPermissionActionExtension.Value != null
                ? typeof(MethodOptions).GetMethod("HasExtension", new[] { CachedPermissionActionExtension.Value.GetType() })
                : null);
        private static readonly Lazy<MethodInfo?> CachedGetExtensionForAction =
            new Lazy<MethodInfo?>(() => CachedPermissionActionExtension.Value != null
                ? typeof(MethodOptions).GetMethod("GetExtension", new[] { CachedPermissionActionExtension.Value.GetType() })
                : null);
        private static readonly Lazy<MethodInfo?> CachedHasExtensionForOpenapi =
            new Lazy<MethodInfo?>(() => CachedOpenapiv2OperationExtension.Value != null
                ? typeof(MethodOptions).GetMethod("HasExtension", new[] { CachedOpenapiv2OperationExtension.Value.GetType() })
                : null);
        private static readonly Lazy<MethodInfo?> CachedGetExtensionForOpenapi =
            new Lazy<MethodInfo?>(() => CachedOpenapiv2OperationExtension.Value != null
                ? typeof(MethodOptions).GetMethod("GetExtension", new[] { CachedOpenapiv2OperationExtension.Value.GetType() })
                : null);

        // Cached reflection type (*Reflection class with FileDescriptor)
        private static readonly Lazy<Type?> CachedReflectionType = 
            new Lazy<Type?>(() => Assembly.GetExecutingAssembly()
                .GetTypes()
                .FirstOrDefault(t => t.Name.EndsWith("Reflection") && 
                                     t.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static) != null));
        private static readonly Lazy<PropertyInfo?> CachedDescriptorProperty =
            new Lazy<PropertyInfo?>(() => CachedReflectionType.Value?.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static));

        private readonly ILogger<AuthorizationInterceptor> _Logger;

        private readonly IAccelByteServiceProvider _ABProvider;

        private readonly string _Namespace;

        public AuthorizationInterceptor(ILogger<AuthorizationInterceptor> logger, IAccelByteServiceProvider abSdkProvider)
        {
            _Logger = logger;
            _ABProvider = abSdkProvider;
            _Namespace = abSdkProvider.Config.Namespace;
        }

        private bool IsInternalMethod(string method)
        {
            // CRITICAL: Dynamically whitelist gRPC internal methods (reflection, health checks)
            // 
            // WHY:
            // These methods are NOT defined in our proto files, so FindMethodDescriptor() returns null.
            // Without this check, the code would skip to "authentication is required" and REJECT these calls.
            // This would break:
            //   - Health checks (Kubernetes liveness/readiness probes)
            //   - gRPC reflection (service discovery, grpcurl, etc.)
            //
            // DO NOT REMOVE unless you want infrastructure failures!
            return method.StartsWith("/grpc.reflection.") || 
                   method.StartsWith("/grpc.health.");
        }

        private MethodDescriptor? FindMethodDescriptor(string fullMethod)
        {
            // Parse /package.ServiceName/MethodName or /ServiceName/MethodName format
            var parts = fullMethod.TrimStart('/').Split('/');
            if (parts.Length != 2)
                return null;

            string serviceFullName = parts[0]; // e.g., "service.Service" or "Service"
            string methodName = parts[1];

            // Extract just the service name (after the last dot, if any)
            string serviceName = serviceFullName.Contains('.') 
                ? serviceFullName.Substring(serviceFullName.LastIndexOf('.') + 1)
                : serviceFullName;

            // Use cached reflection type and descriptor property (computed once)
            var reflectionType = CachedReflectionType.Value;
            if (reflectionType == null)
                return null;

            var descriptorProp = CachedDescriptorProperty.Value;
            if (descriptorProp == null)
                return null;

            var descriptor = descriptorProp.GetValue(null) as Google.Protobuf.Reflection.FileDescriptor;
            
            if (descriptor?.Services == null)
                return null;

            var serviceDesc = descriptor.Services
                .FirstOrDefault(s => s.Name == serviceName);

            if (serviceDesc == null)
                return null;

            return serviceDesc.Methods.FirstOrDefault(m => m.Name == methodName);
        }

        // Find extension classes at runtime (works without compile-time dependencies)
        private static Type? FindExtensionClass(string className)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => 
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        return Array.Empty<Type>();
                    }
                })
                .FirstOrDefault(t => t.Name == className && t.IsClass && t.IsSealed && t.IsAbstract);
        }

        private bool HasBearerSecurity(MethodOptions methodOptions)
        {
            // Use cached extension field and MethodInfo
            var extensionField = CachedOpenapiv2OperationExtension.Value;
            if (extensionField == null)
                return false;

            var hasExtensionMethod = CachedHasExtensionForOpenapi.Value;
            if (hasExtensionMethod == null)
                return false;

            var hasExtension = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { extensionField })!;
            if (!hasExtension)
                return false;

            var getExtensionMethod = CachedGetExtensionForOpenapi.Value;
            if (getExtensionMethod == null)
                return false;

            var operation = getExtensionMethod.Invoke(methodOptions, new[] { extensionField });
            if (operation == null)
                return false;

            var securityProp = operation.GetType().GetProperty("Security");
            if (securityProp == null)
                return false;

            var security = securityProp.GetValue(operation);
            if (security == null)
                return false;

            // Check for "Bearer" in Security collection
            if (security is System.Collections.IEnumerable securityEnumerable)
            {
                foreach (var secReq in securityEnumerable)
                {
                    var securityReqProp = secReq.GetType().GetProperty("SecurityRequirement_");
                    if (securityReqProp == null)
                        continue;

                    var securityReqDict = securityReqProp.GetValue(secReq);
                    if (securityReqDict == null)
                        continue;

                    var containsKeyMethod = securityReqDict.GetType().GetMethod("ContainsKey");
                    if (containsKeyMethod != null)
                    {
                        var containsBearer = (bool)containsKeyMethod.Invoke(securityReqDict, new object[] { "Bearer" })!;
                        if (containsBearer)
                            return true;
                    }
                }
            }

            return false;
        }

        private void ExtractPermissionExtensions(MethodOptions methodOptions, 
            out string permission, out int action)
        {
            permission = "";
            action = 0;

            // Use cached Resource extension and MethodInfo
            var resourceExtension = CachedPermissionResourceExtension.Value;
            if (resourceExtension != null)
            {
                var hasExtensionMethod = CachedHasExtensionForResource.Value;
                var getExtensionMethod = CachedGetExtensionForResource.Value;

                if (hasExtensionMethod != null && getExtensionMethod != null)
                {
                    var hasResource = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { resourceExtension })!;
                    if (hasResource)
                    {
                        var resourceValue = getExtensionMethod.Invoke(methodOptions, new[] { resourceExtension });
                        permission = resourceValue?.ToString() ?? "";
                    }
                }
            }

            // Use cached Action extension and MethodInfo
            var actionExtension = CachedPermissionActionExtension.Value;
            if (actionExtension != null)
            {
                var hasExtensionMethod = CachedHasExtensionForAction.Value;
                var getExtensionMethod = CachedGetExtensionForAction.Value;

                if (hasExtensionMethod != null && getExtensionMethod != null)
                {
                    var hasAction = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { actionExtension })!;
                    if (hasAction)
                    {
                        var actionValue = getExtensionMethod.Invoke(methodOptions, new[] { actionExtension });
                        // Convert enum to int
                        if (actionValue != null)
                        {
                            if (actionValue is Enum enumValue)
                                action = Convert.ToInt32(enumValue);
                            else if (int.TryParse(actionValue.ToString(), out int intValue))
                                action = intValue;
                        }
                    }
                }
            }
        }

        private void Authenticate(ServerCallContext context)
        {
            // Skip authentication for internal gRPC methods (health, reflection)
            if (IsInternalMethod(context.Method))
                return;

            // Find method descriptor using cached reflection type
            MethodDescriptor? methodDesc = FindMethodDescriptor(context.Method);

            bool requiresToken = false;
            string qPermission = "";
            int qAction = 0;

            // Check for Bearer security and permissions (runtime detection, works without dependencies)
            if (methodDesc != null)
            {
                MethodOptions mOpts = methodDesc.GetOptions();

                requiresToken = HasBearerSecurity(mOpts);
                ExtractPermissionExtensions(mOpts, out qPermission, out qAction);

                // Early exit for public endpoints (no Bearer security and no permissions)
                if (!requiresToken && string.IsNullOrEmpty(qPermission) && qAction == 0)
                {
                    return;
                }
            }

            // At this point, authentication is required
            // Get authorization token
            string? authToken = context.RequestHeaders.GetValue("authorization");
            if (authToken == null)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "No authorization token provided."));

            string[] authParts = authToken.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (authParts.Length != 2)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid authorization token format"));

            string token = authParts[1];

            // Validate ExtendNamespace claim
            AccessTokenPayload? payload = _ABProvider.Sdk.ParseAccessToken(token, false);
            if (payload == null)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Could not parse access token payload"));

            if (!string.IsNullOrEmpty(payload.ExtendNamespace) && payload.ExtendNamespace != _Namespace)
                throw new RpcException(new Status(StatusCode.PermissionDenied, 
                    $"Invalid access token for this namespace. Token is for '{payload.ExtendNamespace}', expected '{_Namespace}'"));

            // Replace namespace placeholder in permission string
            qPermission = NamespacePlaceholderRegex.Replace(qPermission, (m) => _ABProvider.Sdk.Namespace);

            // Validate token with or without permissions
            if (!string.IsNullOrEmpty(qPermission) && qAction > 0)
            {
                bool b = _ABProvider.Sdk.ValidateToken(token, qPermission, qAction);
                if (!b)
                    throw new RpcException(new Status(StatusCode.PermissionDenied, 
                        $"Valid access token with permission {qPermission} [{qAction}] is required."));
            }
            else
            {
                bool b = _ABProvider.Sdk.ValidateToken(token);
                if (!b)
                    throw new RpcException(new Status(StatusCode.PermissionDenied, "Valid access token is required."));
            }
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, 
            ServerCallContext context, 
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                Authenticate(context);
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

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request, 
            IServerStreamWriter<TResponse> responseStream, 
            ServerCallContext context, 
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                Authenticate(context);
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

            await continuation(request, responseStream, context);
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, 
            ServerCallContext context, 
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                Authenticate(context);
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

            return await continuation(requestStream, context);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, 
            IServerStreamWriter<TResponse> responseStream, 
            ServerCallContext context, 
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                Authenticate(context);
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

            await continuation(requestStream, responseStream, context);
        }
    }
}


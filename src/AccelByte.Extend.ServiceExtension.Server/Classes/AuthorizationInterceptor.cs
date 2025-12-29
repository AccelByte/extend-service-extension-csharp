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

        private Type? CachedPermissionExtensionsType;
        private Type? CachedAnnotationsExtensionsType;

        private PropertyInfo? CachedPermissionResourceProp;
        private PropertyInfo? CachedPermissionActionProp;
        private PropertyInfo? CachedOpenapiv2OperationProp;

        private object? CachedPermissionResourceExtension;
        private object? CachedPermissionActionExtension;
        private object? CachedOpenapiv2OperationExtension;

        private MethodInfo? CachedHasExtensionForResource;
        private MethodInfo? CachedGetExtensionForResource;
        private MethodInfo? CachedHasExtensionForAction;
        private MethodInfo? CachedGetExtensionForAction;
        private MethodInfo? CachedHasExtensionForOpenapi;
        private MethodInfo? CachedGetExtensionForOpenapi;

        private Type? CachedReflectionType;
        private PropertyInfo? CachedDescriptorProperty;

        private readonly ILogger<AuthorizationInterceptor> _Logger;

        private readonly IAccelByteServiceProvider _ABProvider;

        private readonly string _Namespace;

        public AuthorizationInterceptor(ILogger<AuthorizationInterceptor> logger, IAccelByteServiceProvider abSdkProvider)
        {
            _Logger = logger;
            _ABProvider = abSdkProvider;
            _Namespace = abSdkProvider.Config.Namespace;
            
            InitializeReflectionCache();
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
            var parts = fullMethod.TrimStart('/').Split('/');
            if (parts.Length != 2) return null;

            string serviceFullName = parts[0];
            string methodName = parts[1];

            if (string.IsNullOrEmpty(serviceFullName) || string.IsNullOrEmpty(methodName)) return null;

            string serviceName = serviceFullName.Contains('.') 
                ? serviceFullName.Substring(serviceFullName.LastIndexOf('.') + 1)
                : serviceFullName;

            if (string.IsNullOrEmpty(serviceName)) return null;

            if (CachedReflectionType == null) return null;

            if (CachedDescriptorProperty == null) return null;

            FileDescriptor? descriptor;
            try
            {
                descriptor = CachedDescriptorProperty.GetValue(null) as FileDescriptor;
            }
            catch (Exception ex)
            {
                _Logger.LogWarning(ex, "Failed to get FileDescriptor from reflection type");
                return null;
            }
            
            if (descriptor?.Services == null) return null;

            var matchingServices = descriptor.Services
                .Where(s => s.Name == serviceName)
                .ToList();

            if (matchingServices.Count == 0) return null;

            if (matchingServices.Count > 1)
            {
                _Logger.LogWarning("Multiple services found with name '{ServiceName}' ({Count} matches). Using first match.", 
                    serviceName, matchingServices.Count);
            }

            var serviceDesc = matchingServices[0];
            return serviceDesc.Methods.FirstOrDefault(m => m.Name == methodName);
        }

        private static Type? FindExtensionClass(string className, ILogger<AuthorizationInterceptor>? logger)
        {
            logger?.LogDebug("Searching for extension class: {ClassName}", className);
            
            bool HasExpectedProperties(Type type)
            {
                if (className == "PermissionExtensions")
                {
                    var resourceProp = type.GetProperty("Resource", BindingFlags.Public | BindingFlags.Static);
                    var actionProp = type.GetProperty("Action", BindingFlags.Public | BindingFlags.Static);
                    bool hasProps = resourceProp != null && actionProp != null;
                    if (hasProps)
                    {
                        logger?.LogDebug("Found PermissionExtensions candidate: {FullName} (has Resource and Action properties)", type.FullName);
                    }
                    return hasProps;
                }
                else if (className == "AnnotationsExtensions")
                {
                    var openapiProp = type.GetProperty("Openapiv2Operation", BindingFlags.Public | BindingFlags.Static);
                    bool hasProp = openapiProp != null;
                    if (hasProp)
                    {
                        logger?.LogDebug("Found AnnotationsExtensions candidate: {FullName} (has Openapiv2Operation property)", type.FullName);
                    }
                    return hasProp;
                }
                
                return false;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            logger?.LogDebug("Scanning {AssemblyCount} assemblies for {ClassName}", assemblies.Length, className);
            
            var allTypes = assemblies
                .SelectMany(a => 
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        logger?.LogWarning(ex, "Failed to load types from assembly: {AssemblyName}", a.FullName);
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => 
                    t.Name == className && 
                    t.IsClass && 
                    t.IsSealed && 
                    t.IsAbstract &&
                    HasExpectedProperties(t))
                .ToList();

            if (allTypes.Count == 0)
            {
                logger?.LogWarning("Extension class not found: {ClassName}. Permission/OpenAPI features may be unavailable.", className);
                return null;
            }
            
            if (allTypes.Count > 1)
            {
                logger?.LogWarning(
                    "Multiple matches found for {ClassName} ({Count} matches). Using first match: {FirstMatch}. Others: {OtherMatches}",
                    className, 
                    allTypes.Count,
                    allTypes[0].FullName,
                    string.Join(", ", allTypes.Skip(1).Select(t => t.FullName)));
            }
            else
            {
                logger?.LogInformation("Found extension class: {ClassName} -> {FullName}", className, allTypes[0].FullName);
            }

            return allTypes.FirstOrDefault();
        }

        private static Type? FindReflectionType(ILogger<AuthorizationInterceptor>? logger)
        {
            logger?.LogDebug("Searching for *Reflection class with FileDescriptor");
            
            try
            {
                var reflectionType = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .FirstOrDefault(t => t.Name.EndsWith("Reflection") &&
                                         t.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static) != null);
                
                if (reflectionType == null) { logger?.LogWarning("Reflection type not found. Method descriptor lookup may fail."); }
                else { logger?.LogInformation("Found reflection type: {FullName}", reflectionType.FullName); }
                
                return reflectionType;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error searching for reflection type");
                return null;
            }
        }

        private void InitializeReflectionCache()
        {
            _Logger.LogDebug("Initializing reflection cache for AuthorizationInterceptor");
            
            CachedPermissionExtensionsType = FindExtensionClass("PermissionExtensions", _Logger);
            CachedAnnotationsExtensionsType = FindExtensionClass("AnnotationsExtensions", _Logger);
            CachedReflectionType = FindReflectionType(_Logger);

            CachedPermissionResourceProp = CachedPermissionExtensionsType?.GetProperty("Resource",
                BindingFlags.Public | BindingFlags.Static);
            CachedPermissionActionProp = CachedPermissionExtensionsType?.GetProperty("Action",
                BindingFlags.Public | BindingFlags.Static);
            CachedOpenapiv2OperationProp = CachedAnnotationsExtensionsType?.GetProperty("Openapiv2Operation",
                BindingFlags.Public | BindingFlags.Static);

            try { CachedPermissionResourceExtension = CachedPermissionResourceProp?.GetValue(null); }
            catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get PermissionResource extension value"); }

            try { CachedPermissionActionExtension = CachedPermissionActionProp?.GetValue(null); }
            catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get PermissionAction extension value"); }

            try { CachedOpenapiv2OperationExtension = CachedOpenapiv2OperationProp?.GetValue(null); }
            catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get Openapiv2Operation extension value"); }

            if (CachedPermissionResourceExtension != null)
            {
                try
                {
                    CachedHasExtensionForResource = typeof(MethodOptions).GetMethod("HasExtension",
                        new[] { CachedPermissionResourceExtension.GetType() });
                    if (CachedHasExtensionForResource == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for HasExtension method (PermissionResource)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get HasExtension method for PermissionResource");
                }

                try
                {
                    CachedGetExtensionForResource = typeof(MethodOptions).GetMethod("GetExtension",
                        new[] { CachedPermissionResourceExtension.GetType() });
                    if (CachedGetExtensionForResource == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for GetExtension method (PermissionResource)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get GetExtension method for PermissionResource");
                }
            }

            if (CachedPermissionActionExtension != null)
            {
                try
                {
                    CachedHasExtensionForAction = typeof(MethodOptions).GetMethod("HasExtension",
                        new[] { CachedPermissionActionExtension.GetType() });
                    if (CachedHasExtensionForAction == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for HasExtension method (PermissionAction)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get HasExtension method for PermissionAction");
                }

                try
                {
                    CachedGetExtensionForAction = typeof(MethodOptions).GetMethod("GetExtension",
                        new[] { CachedPermissionActionExtension.GetType() });
                    if (CachedGetExtensionForAction == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for GetExtension method (PermissionAction)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get GetExtension method for PermissionAction");
                }
            }

            if (CachedOpenapiv2OperationExtension != null)
            {
                try
                {
                    CachedHasExtensionForOpenapi = typeof(MethodOptions).GetMethod("HasExtension",
                        new[] { CachedOpenapiv2OperationExtension.GetType() });
                    if (CachedHasExtensionForOpenapi == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for HasExtension method (Openapiv2Operation)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get HasExtension method for Openapiv2Operation");
                }

                try
                {
                    CachedGetExtensionForOpenapi = typeof(MethodOptions).GetMethod("GetExtension",
                        new[] { CachedOpenapiv2OperationExtension.GetType() });
                    if (CachedGetExtensionForOpenapi == null)
                    {
                        _Logger.LogWarning("GetMethod returned null for GetExtension method (Openapiv2Operation)");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogWarning(ex, "Failed to get GetExtension method for Openapiv2Operation");
                }
            }

            CachedDescriptorProperty = CachedReflectionType?.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
            
            if (CachedPermissionExtensionsType != null)
            {
                _Logger.LogInformation("Found PermissionExtensions: {FullName}", CachedPermissionExtensionsType.FullName);
                _Logger.LogDebug("PermissionExtensions properties - Resource: {HasResource}, Action: {HasAction}",
                    CachedPermissionResourceProp != null, CachedPermissionActionProp != null);
            }
            else { _Logger.LogWarning("PermissionExtensions not found. Permission features may be unavailable."); }
            
            if (CachedAnnotationsExtensionsType != null)
            {
                _Logger.LogInformation("Found AnnotationsExtensions: {FullName}", CachedAnnotationsExtensionsType.FullName);
                _Logger.LogDebug("AnnotationsExtensions properties - Openapiv2Operation: {HasOpenapi}",
                    CachedOpenapiv2OperationProp != null);
            }
            else
            {
                _Logger.LogWarning("AnnotationsExtensions not found. OpenAPI Bearer security features may be unavailable.");
            }
            
            if (CachedReflectionType != null)
            {
                _Logger.LogInformation("Found reflection type: {FullName}", CachedReflectionType.FullName);
                _Logger.LogDebug("Reflection type properties - Descriptor: {HasDescriptor}",
                    CachedDescriptorProperty != null);
            }
            else { _Logger.LogWarning("Reflection type not found. Method descriptor lookup may fail."); }
            
            _Logger.LogInformation("Reflection cache initialization complete");
        }

        private bool HasBearerSecurity(MethodOptions methodOptions)
        {
            try
            {
                var extensionField = CachedOpenapiv2OperationExtension;
                if (extensionField == null) return false;

                var hasExtensionMethod = CachedHasExtensionForOpenapi;
                if (hasExtensionMethod == null) return false;

                bool hasExtension;
                try { hasExtension = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { extensionField })!; }
                catch (Exception ex) { _Logger.LogWarning(ex, "Failed to check for OpenAPI extension"); return false; }

                if (!hasExtension) return false;

                var getExtensionMethod = CachedGetExtensionForOpenapi;
                if (getExtensionMethod == null) return false;

                object? operation;
                try { operation = getExtensionMethod.Invoke(methodOptions, new[] { extensionField }); }
                catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get OpenAPI operation extension"); return false; }

                if (operation == null) return false;

                // Note: GetProperty/GetMethod calls here are acceptable performance-wise
                // as they're only executed during authentication checks, not on every request
                PropertyInfo? securityProp;
                try { securityProp = operation.GetType().GetProperty("Security"); }
                catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get Security property"); return false; }

                if (securityProp == null) return false;

                object? security;
                try { security = securityProp.GetValue(operation); }
                catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get Security value"); return false; }

                if (security == null) return false;

                if (security is System.Collections.IEnumerable securityEnumerable)
                {
                    foreach (var secReq in securityEnumerable)
                    {
                        PropertyInfo? securityReqProp;
                        try { securityReqProp = secReq.GetType().GetProperty("SecurityRequirement_"); }
                        catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get SecurityRequirement_ property"); continue; }

                        if (securityReqProp == null) continue;

                        object? securityReqDict;
                        try { securityReqDict = securityReqProp.GetValue(secReq); }
                        catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get SecurityRequirement_ value"); continue; }

                        if (securityReqDict == null) continue;

                        MethodInfo? containsKeyMethod;
                        try { containsKeyMethod = securityReqDict.GetType().GetMethod("ContainsKey"); }
                        catch (Exception ex) { _Logger.LogWarning(ex, "Failed to get ContainsKey method"); continue; }

                        if (containsKeyMethod != null)
                        {
                            bool containsBearer;
                            try { containsBearer = (bool)containsKeyMethod.Invoke(securityReqDict, new object[] { "Bearer" })!; }
                            catch (Exception ex) { _Logger.LogWarning(ex, "Failed to check for Bearer security"); continue; }

                            if (containsBearer) return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _Logger.LogWarning(ex, "Error checking Bearer security");
                return false;
            }
        }

        private void ExtractPermissionExtensions(MethodOptions methodOptions, 
            out string permission, out int action)
        {
            permission = "";
            action = 0;

            var resourceExtension = CachedPermissionResourceExtension;
            if (resourceExtension != null)
            {
                var hasExtensionMethod = CachedHasExtensionForResource;
                var getExtensionMethod = CachedGetExtensionForResource;

                if (hasExtensionMethod != null && getExtensionMethod != null)
                {
                    try
                    {
                        var hasResource = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { resourceExtension })!;
                        if (hasResource)
                        {
                            var resourceValue = getExtensionMethod.Invoke(methodOptions, new[] { resourceExtension });
                            permission = resourceValue?.ToString() ?? "";
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogWarning(ex, "Failed to extract permission resource extension");
                    }
                }
            }

            var actionExtension = CachedPermissionActionExtension;
            if (actionExtension != null)
            {
                var hasExtensionMethod = CachedHasExtensionForAction;
                var getExtensionMethod = CachedGetExtensionForAction;

                if (hasExtensionMethod != null && getExtensionMethod != null)
                {
                    try
                    {
                        var hasAction = (bool)hasExtensionMethod.Invoke(methodOptions, new[] { actionExtension })!;
                        if (hasAction)
                        {
                            var actionValue = getExtensionMethod.Invoke(methodOptions, new[] { actionExtension });
                            if (actionValue != null)
                            {
                                if (actionValue is Enum enumValue) action = Convert.ToInt32(enumValue);
                                else if (int.TryParse(actionValue.ToString(), out int intValue))
                                    action = intValue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogWarning(ex, "Failed to extract permission action extension");
                    }
                }
            }
        }

        private void ValidateToken(string token, string permission, int action)
        {
            bool isValid;
            try
            {
                if (!string.IsNullOrEmpty(permission) && action > 0) { isValid = _ABProvider.Sdk.ValidateToken(token, permission, action); }
                else { isValid = _ABProvider.Sdk.ValidateToken(token); }
            }
            catch (Exception ex)
            {
                _Logger.LogWarning(ex, "Failed to validate token{WithPermissions}", 
                    !string.IsNullOrEmpty(permission) && action > 0 ? " with permissions" : "");
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
            }

            if (!isValid) throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));
        }

        private void Authenticate(ServerCallContext context)
        {
            // Skip authentication for internal gRPC methods (health, reflection)
            if (IsInternalMethod(context.Method)) return;

            MethodDescriptor? methodDesc = FindMethodDescriptor(context.Method);

            bool requiresToken = false;
            string permission = "";
            int action = 0;

            if (methodDesc != null)
            {
                MethodOptions mOpts = methodDesc.GetOptions();

                requiresToken = HasBearerSecurity(mOpts);
                ExtractPermissionExtensions(mOpts, out permission, out action);

                if (!requiresToken && string.IsNullOrEmpty(permission) && action == 0) return;
            }

            string? authToken = context.RequestHeaders.GetValue("authorization");
            if (authToken == null) throw new RpcException(new Status(StatusCode.Unauthenticated, "Authorization required"));

            string[] authParts = authToken.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (authParts.Length != 2 || authParts[0] != "Bearer")
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid authorization header format"));

            string token = authParts[1];
            if (string.IsNullOrEmpty(token)) throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid authorization header format"));

            AccessTokenPayload? payload;
            try { payload = _ABProvider.Sdk.ParseAccessToken(token, false); }
            catch (Exception ex)
            {
                _Logger.LogWarning(ex, "Failed to parse access token");
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid access token"));
            }

            if (payload == null) throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid access token"));

            if (!string.IsNullOrEmpty(payload.ExtendNamespace) && payload.ExtendNamespace != _Namespace)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Access denied"));

            permission = NamespacePlaceholderRegex.Replace(permission, (m) => _ABProvider.Sdk.Namespace);
            ValidateToken(token, permission, action);
        }

        private void HandleAuthentication(ServerCallContext context)
        {
            try { Authenticate(context); }
            catch (RpcException) { throw; }
            catch (ArgumentException ex)
            {
                _Logger.LogWarning(ex, "Invalid argument during authentication");
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid request"));
            }
            catch (UnauthorizedAccessException ex)
            {
                _Logger.LogWarning(ex, "Unauthorized access attempt");
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Authorization required"));
            }
            catch (Exception ex)
            {
                _Logger.LogError(ex, "Unexpected error during authentication: {ExceptionType}", ex.GetType().Name);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, 
            ServerCallContext context, 
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            HandleAuthentication(context);
            return await continuation(request, context);
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request, 
            IServerStreamWriter<TResponse> responseStream, 
            ServerCallContext context, 
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            HandleAuthentication(context);
            await continuation(request, responseStream, context);
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, 
            ServerCallContext context, 
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            HandleAuthentication(context);
            return await continuation(requestStream, context);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, 
            IServerStreamWriter<TResponse> responseStream, 
            ServerCallContext context, 
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            HandleAuthentication(context);
            await continuation(requestStream, responseStream, context);
        }
    }
}


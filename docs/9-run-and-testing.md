#  Chapter 9: Running and Testing the Service

In this chapter, we will go over how to run your Guild Service and perform some basic tests to 
ensure that everything is working as expected.

# 9.1 Running the Service

## Setup

To be able to run this sample app, you will need to follow these setup steps.

- Create a docker compose `.env` file by copying the content of [.env.template](.env.template) file.

- Fill in the required environment variables in `.env` file as shown below.

   ```txt
   AB_BASE_URL=https://demo.accelbyte.io      # Base URL of AccelByte Gaming Services demo environment
   AB_CLIENT_ID='xxxxxxxxxx'                  # Use Client ID from the Setup section
   AB_CLIENT_SECRET='xxxxxxxxxx'              # Use Client Secret from the Setup section
   AB_NAMESPACE='xxxxxxxxxx'                  # Use Namespace ID from the Setup section
   PLUGIN_GRPC_SERVER_AUTH_ENABLED=true       # Enable or disable access token and permission verification
   ```

  > :info: **PLUGIN_GRPC_SERVER_AUTH_ENABLED**: If 'disable' will bypass the validation being set on the endpoint `permission.action` and `permission.resource` [creating-new-endpoint](6-creating-new-endpoint.md#6-creating-a-new-endpoint)

- Ensure you have configured all required permission for your clientId, in this custom service we're using:
  > :exclamation: For AGS Starter customers, you don't need to add the permissions. All confidential IAM clients already contain the necessary permissions.
  - ADMIN:ROLE [READ]
    - It was needed since we define our permission as `ADMIN:` in the guildService.proto
  - ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE,READ,UPDATE,DELETE]
    - It was needed since we access cloudsave game record endpoint which requires the above permission

- (Optional) `grpc-gateway-dependencies` mentioned in [chapter 4](4-installation-and-setup.md) is up and running if you needed the observability stack

## Change API base path

To change the base path you need to change the base path 2 places

- in `gateway/pkg/common/config.go`, to be accurately this part
```go
BasePath    = "/guild"
```

- in `src/AccelByte.PluginArch.ServiceExtension.Demo.Server/Protos/guildService.proto`
```protobuf
// OpenAPI options for the entire API.
option (grpc.gateway.protoc_gen_openapiv2.options.openapiv2_swagger) = {
  // ...
  
  base_path: "/guild";
  
  // ...
};

```

## Building, Running, and Testing

Please see [README.md](../README.md).

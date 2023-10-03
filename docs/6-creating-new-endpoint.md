# 6. Creating a New Endpoint

In this chapter, we will be adding new endpoints to our service. This involves three main steps:

1. Defining the service and its methods in our `.proto` file.
2. Generating gRPC-gateway code from the updated `.proto` file.

## 6.1 Defining the Service in the `.proto` File

gRPC services and messages are defined in `.proto` files. Our `.proto` file is located in `src/AccelByte.PluginArch.ServiceExtension.Demo.Server/Protos`. Let's add new service methods to our `GuildService`:

```protobuf
service GuildService {

  rpc CreateOrUpdateGuildProgress (CreateOrUpdateGuildProgressRequest) returns (CreateOrUpdateGuildProgressResponse) {
    option (permission.action) = CREATE;
    option (permission.resource) = "ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD";
    option (google.api.http) = {
      post: "/v1/admin/namespace/{namespace}/progress"
      body: "*"
    };
    option (grpc.gateway.protoc_gen_openapiv2.options.openapiv2_operation) = {
      summary: "Update Guild progression"
      description: "Update Guild progression if not existed yet will create a new one"
      security: {
        security_requirement: {
          key: "Bearer"
          value: {}
        }
      }
    };
  }

  rpc GetGuildProgress (GetGuildProgressRequest) returns (GetGuildProgressResponse) {
    option (permission.action) = READ;
    option (permission.resource) = "ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD";
    option (google.api.http) = {
      get: "/v1/admin/namespace/{namespace}/progress/{guild_id}"
    };
    option (grpc.gateway.protoc_gen_openapiv2.options.openapiv2_operation) = {
      summary: "Get guild progression"
      description: "Get guild progression"
      security: {
        security_requirement: {
          key: "Bearer"
          value: {}
        }
      }
    };
  }
}

message CreateOrUpdateGuildProgressRequest {
  string namespace = 1;
  GuildProgress guild_progress = 2;
}

message CreateOrUpdateGuildProgressResponse {
  GuildProgress guild_progress = 1;
}

message GetGuildProgressRequest {
  string namespace = 1;
  string guild_id = 2;
}

message GetGuildProgressResponse {
  GuildProgress guild_progress = 1;
}

// OpenAPI options for the entire API.
option (grpc.gateway.protoc_gen_openapiv2.options.openapiv2_swagger) = {
  info: {
    title: "Guild Service API";
    version: "1.0";
  };
  schemes: HTTP;
  schemes: HTTPS;
  base_path: "/guild";

  security_definitions: {
    security: {
      key: "Bearer";
      value: {
        type: TYPE_API_KEY;
        in: IN_HEADER;
        name: "Authorization";
      }
    }
  };
};
```

In this case, we've added two service methods: `CreateOrUpdateGuildProgress` and `GetGuildProgress`.

In the `CreateOrUpdateGuildProgress` method, we use the `option (google.api.http)` annotation to specify the HTTP method and path for this service method. The post: `"/v1/admin/namespace/{namespace}/progress"` means that this service method will be exposed as a POST HTTP method at the path `"/v1/admin/namespace/{namespace}/progress"`.

We use body: `"*"` to indicate that the entire request message will be used as the HTTP request body. Alternatively, you could specify a particular field of the request message to be used as the HTTP request body.

The `option (grpc.gateway.protoc_gen_openapiv2.options.openapiv2_operation)` annotation is used for additional metadata about the operation that can be used by tools like Swagger.

Similarly, in the `GetGuildProgress` method, we specify a GET HTTP method and the path includes a variable part `{guild_id}` which will be substituted with the actual `guild_id` in the HTTP request.

Permission control via `permission.proto`
  Annotations for fine-grained access control:

- `permission.action`: it can be either READ, CREATE, UPDATE, DELETE
- `permission.resource`: Defines scope-based access control (e.g., ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD).

After defining the service and methods in the `.proto` file, build the service project using `dotnet build` inside the service project directory. This will re-generate all grpc related models and service class. Then we run the protoc compiler to generate the corresponding grpc-gateway code.

> gRPC Gateway tricky part: `base_path`. If `base_path` is set, note that it doesn't alter the paths generated in the Swagger file. Your actual API paths in `google.api.http` remain unchanged. If you're using `base_path`, you'll need to manually adjust the `BasePath` in the `gateway/pkg/common/config.go`. We will explain more about this in the following chapter.

## 6.2 Generating gRPC Gateway Go Code

After updating our .proto file, we need to generate Go code from it.
The protobuf compiler `protoc` is used to generate Go code from our .proto file. 
However, in our setup, we've simplified this with a `Makefile`.

```bash
make gen-gateway
```

> :warning: This action will clear all files inside `gateway/pkg/pb`. This directory is reserved for grpc-gateway auto-generated code. Do not put your code inside this directory as it will be removed if you run this action.

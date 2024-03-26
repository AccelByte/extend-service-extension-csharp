# 5. Project Structure

This chapter offers an overview of the Guild Service's project structure. Understanding this structure will help you navigate the project and identify where to make changes as you add new features or fix bugs.

```bash
.
├── docs
├── gateway                                                 # gRPC Gateway code (GoLang)
├── src
│   ├── AccelByte.PluginArch.ServiceExtension.Demo.Server   # Service's project
│   │   ├── Classes                                         # Put your misc classes here
│   │   ├── Model                                           # Put your data model classes here
│   │   ├── Protos                                          # Your protobuf folder
│   │   ├── Services                                        # Your gRPC Server implementation here
│   │   ├── appsettings.json                                # Default configuration (do not put confidential values here)
│   │   └── Program.cs                                      # Entrypoint
│   └── AccelByte.PluginArch.ServiceExtension.Demo.Tests    # Service's Unit Test
├── Dockerfile                                              # To build complete image with service and grpc-gateway
├── docker-compose.yaml                                     # Compose file that use complete image
└── Makefile
```

The most important files and directories are:

- `Makefile`: This file contains scripts that automate tasks like building our service, running tests, and cleaning up.
- All `Dockerfile`: Dockerfile(s) for our service. This is used by Docker to build a container image for our service.
- All `docker-compose.yaml`: Defines the services that make up our application, so they can be run together using Docker Compose.
- `src`: Contains service's project and unit test.
- `gateway`: Contains grpc-gateway code. Visit [extend-service-extension-go](https://github.com/AccelByte/extend-service-extension-go) for more information on this.

In the following chapters, we will discuss how to define and implement new services and messages in our `.proto` files, how to generate grpc-gateway code from these `.proto` files, and how to implement these services in our server.
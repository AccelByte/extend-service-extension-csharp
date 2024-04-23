# Chapter 7: Writing Service Implementations

Now that we have defined our service, the next step is to implement our service. 
This is where we define the actual logic of our gRPC methods.
You can read more information related to .NET gRPC ASPNET Core [here](https://learn.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-6.0).

We'll be doing this in the `src/AccelByte.Extend.ServiceExtension.Server/Services/MyService.cs` file.

Here's a brief outline of what this chapter will cover:

## 7.1 Setting Up the Guild Service

### 7.1 Setting Up the Guild Service
To set up our guild service, we'll first create a class derived from `Service.ServiceBase`. This class will act as our service implementation.

```csharp
using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Grpc.Core;
using AccelByte.Sdk.Api;
using AccelByte.Extend.ServiceExtension.Server.Model;

namespace AccelByte.Extend.ServiceExtension.Server.Services
{
    public class MyService : Service.ServiceBase
    {
        public MyService()
        {
                                    
        }

        //implement your service logic in here
    }
}

```

To implement the `CreateOrUpdateGuildProgress` function, you can override the method like this:
```csharp
public override Task<CreateOrUpdateGuildProgressResponse> CreateOrUpdateGuildProgress(CreateOrUpdateGuildProgressRequest request, ServerCallContext context)
{
    // Implementation goes here
}
```

And similarly for the `GetGuildProgress`` function:

```csharp
public override Task<GetGuildProgressResponse> GetGuildProgress(GetGuildProgressRequest request, ServerCallContext context)
{
    // Implementation goes here
}
```

In these methods, you would include the logic to interact with CloudSave or 
any other dependencies in order to process the requests.
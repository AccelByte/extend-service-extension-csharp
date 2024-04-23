# Chapter 8: Integrating with AccelByte's CloudSave

In this chapter, we'll learn how to integrate the AccelByte's CloudSave feature into our GuildService.

## 8.1. Understanding CloudSave

AccelByte's CloudSave is a cloud-based service that enables you to save and retrieve game data in 
a structured manner. It allows for easy and quick synchronization of player data across different 
devices. This can be especially useful in multiplayer games where players' data needs to be synced 
in real-time. Please refer to our docs portal for more details

## 8.2. Setting up CloudSave

The first step to using CloudSave is setting it up. 
In the context of our GuildService, this involve adding .NET (C#) AccelByteSdk [package](https://www.nuget.org/packages/AccelByte.Sdk/) to our service and bootstrap it.
We will use dependency injection to supply the AccelByteSDK object to our service implementation class.

1. Add .NET (C#) AccelByteSdk package using `dotnet add AccelByte.Sdk` inside service project directory (or you can use "Manage NuGet Packages" in Visual Studio).

2. Create `AppSettingConfigRepository` class as a configuration model for our service. Later we can associate its properties in `appsettings.json`. Read more about ASPNET.Core configuration [here](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0)
```csharp
using System;
using Microsoft.Extensions.Configuration;

using AccelByte.Sdk.Core.Logging;
using AccelByte.Sdk.Core.Repository;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public class AppSettingConfigRepository : IConfigRepository
    {
        public string BaseUrl { get; set; } = String.Empty;

        public string ClientId { get; set; } = String.Empty;

        public string ClientSecret { get; set; } = String.Empty;

        public string AppName { get; set; } = String.Empty;

        public string TraceIdVersion { get; set; } = String.Empty;

        public string Namespace { get; set; } = String.Empty;

        public bool EnableTraceId { get; set; } = false;

        public bool EnableUserAgentInfo { get; set; } = false;

        public string ResourceName { get; set; } = String.Empty;

        public IHttpLogger? Logger { get; set; } = null;

        public void ReadEnvironmentVariables()
        {
            //read environment variable logic
        }
    }
}
```

2. Add interface `IAccelByteServiceProvider` inside `Classes` directory.
```csharp
using System;
using AccelByte.Sdk.Core;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public interface IAccelByteServiceProvider
    {
        AccelByteSDK Sdk { get; }

        AppSettingConfigRepository Config { get; }
    }
}
```

3. Add implementation `DefaultAccelByteServiceProvider` class for our `IAccelByteServiceProvider` interface inside `Classes` directory.
```csharp
using System;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using AccelByte.Sdk.Core;

namespace AccelByte.Extend.ServiceExtension.Server
{
    public class DefaultAccelByteServiceProvider : IAccelByteServiceProvider
    {
        private ILogger<DefaultAccelByteServiceProvider> _Logger;

        public AccelByteSDK Sdk { get; }

        public AppSettingConfigRepository Config { get; }

        public DefaultAccelByteServiceProvider(IConfiguration config, ILogger<DefaultAccelByteServiceProvider> logger)
        {
            _Logger = logger;

            //load configuration from appsettings.json file
            AppSettingConfigRepository? abConfig = config.GetSection("AccelByte").Get<AppSettingConfigRepository>();
            if (abConfig == null)
                throw new Exception("Missing AccelByte configuration section.");
            abConfig.ReadEnvironmentVariables();
            Config = abConfig;

            Sdk = AccelByteSDK.Builder
                .SetConfigRepository(Config)
                .UseDefaultCredentialRepository()
                .UseDefaultHttpClient()
                .UseDefaultTokenRepository()
                .UseAutoTokenRefresh()
                .Build();

            Sdk.LoginClient();
        }
    }
}
```

4. Update our service implementation class.
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Grpc.Core;
using AccelByte.Custom.Guild;
using AccelByte.Sdk.Api;
using AccelByte.Extend.ServiceExtension.Server.Model;

namespace AccelByte.Extend.ServiceExtension.Server.Services
{
    public class MyService : Service.ServiceBase
    {
        private readonly IAccelByteServiceProvider _ABProvider;

        public SampleGuildService(IAccelByteServiceProvider abProvider)
        {
            _ABProvider = abProvider;
        }

        public override Task<CreateOrUpdateGuildProgressResponse> CreateOrUpdateGuildProgress(CreateOrUpdateGuildProgressRequest request, ServerCallContext context)
        {
            //implementation here
        }

        public override Task<GetGuildProgressResponse> GetGuildProgress(GetGuildProgressRequest request, ServerCallContext context)
        {
            //implementation here
        }
    }
}
```

5. Register `IAccelByteServiceProvider` for dependency injection. Usually this location inside webapp builder logic. In this case, in `Program.cs`. By this, our AccelByteSDK object will be initialized and configured when our service implementation class is loaded.
```csharp
    builder.Services.AddSingleton<IAccelByteServiceProvider, DefaultAccelByteServiceProvider>()
```

## 8.3. Using CloudSave in GuildService

Let's go over an example of how we use CloudSave within our GuildService.

When updating the guild progress, after performing any necessary validations and computations, 
you would save the updated progress to CloudSave like so:

```csharp
public override Task<CreateOrUpdateGuildProgressResponse> CreateOrUpdateGuildProgress(CreateOrUpdateGuildProgressRequest request, ServerCallContext context)
{
    string actualGuildId = request.GuildProgress.GuildId.Trim();
    if (actualGuildId == "")
        actualGuildId = Guid.NewGuid().ToString().Replace("-", "");

    string gpKey = $"guildProgress_{actualGuildId}";
    var gpValue = GuildProgressData.FromGuildProgressGrpcData(request.GuildProgress);
    gpValue.GuildId = actualGuildId;

    var response = _ABProvider.Sdk.Cloudsave.AdminGameRecord.AdminPostGameRecordHandlerV1Op
        .Execute<GuildProgressData>(gpValue, gpKey, request.Namespace);
    if (response == null)
        throw new Exception("NULL response from cloudsave service.");

    GuildProgressData savedData = response.Value!;

    return Task.FromResult(new CreateOrUpdateGuildProgressResponse()
    {
        GuildProgress = savedData.ToGuildProgressGrpcData()
    });
}
```

For more accurate details how it was implemented please refer to [src/AccelByte.Extend.ServiceExtension.Server/Services/MyService.cs](src/AccelByte.Extend.ServiceExtension.Server/Services/MyService.cs)

That's it! You've now integrated AccelByte's CloudSave into your GuildService. 
You can now use CloudSave to save and retrieve guild progress, along with any other 
data you might need to store.
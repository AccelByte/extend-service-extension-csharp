// Copyright (c) 2022-2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Tests
{
    public class UnitTestCallContext : ServerCallContext
    {
        public Metadata? ResponseHeaders { get; private set; }

        public UnitTestCallContext()
        {

        }

        protected override string MethodCore { get; } = "UnitTestMethod";

        protected override string HostCore { get; } = "UnitTestHost";

        protected override string PeerCore { get; } = "UnitTestPeer";

        protected override DateTime DeadlineCore { get; } = DateTime.Now.AddDays(30);

        protected override Metadata RequestHeadersCore { get; } = new Metadata();

        protected override CancellationToken CancellationTokenCore { get; } = CancellationToken.None;

        protected override Metadata ResponseTrailersCore { get; } = new Metadata();

        protected override Status StatusCore { get; set; }

        protected override WriteOptions? WriteOptionsCore { get; set; } = null;

        protected override AuthContext AuthContextCore { get; }
            = new AuthContext(string.Empty, new Dictionary<string, List<AuthProperty>>());

        protected override IDictionary<object, object> UserStateCore { get; }
            = new Dictionary<object, object>();

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        {
            throw new NotImplementedException();
        }

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            if (ResponseHeaders != null)
                throw new InvalidOperationException("ResponseHeaders is NOT NULL");

            ResponseHeaders = responseHeaders;
            return Task.CompletedTask;
        }
    }
}

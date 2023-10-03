// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AccelByte.Custom.Guild;
using AccelByte.Sdk.Api.Cloudsave.Model;

namespace AccelByte.PluginArch.ServiceExtension.Demo.Server.Model
{
    public class GuildProgressData : ModelsGameRecordRequest
    {
        [JsonPropertyName("guild_id")]
        public string GuildId { get; set; } = String.Empty;

        [JsonPropertyName("namespace")]
        public string Namespace { get; set; } = String.Empty;

        [JsonPropertyName("objectives")]
        public Dictionary<string, int> Objectives { get; set; } = new();

        public static GuildProgressData FromGuildProgressGrpcData(GuildProgress src)
        {
            return new GuildProgressData()
            {
                GuildId = src.GuildId,
                Namespace = src.Namespace,
                Objectives = new Dictionary<string, int>(src.Objectives)
            };
        }

        public GuildProgress ToGuildProgressGrpcData()
        {
            GuildProgress data = new GuildProgress()
            {
                GuildId = GuildId,
                Namespace = Namespace
            };

            data.Objectives.Add(Objectives);
            return data;
        }
    }
}

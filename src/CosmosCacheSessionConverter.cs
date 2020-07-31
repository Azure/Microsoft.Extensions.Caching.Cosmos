//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class CosmosCacheSessionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(CosmosCacheSession);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonReaderException();
            }
            
            JObject jObject = JObject.Load(reader);
            CosmosCacheSession cosmosCacheSession = new CosmosCacheSession();

            if (!jObject.TryGetValue("id", out JToken idJToken))
            {
                throw new JsonReaderException("Missing id on Cosmos DB session item.");
            }

            cosmosCacheSession.SessionKey = idJToken.Value<string>();

            if (!jObject.TryGetValue("content", out JToken contentJToken))
            {
                throw new JsonReaderException("Missing id on Cosmos DB session item.");
            }

            cosmosCacheSession.Content = contentJToken.Value<byte[]>();

            if (jObject.TryGetValue("ttl", out JToken ttlJToken))
            {
                cosmosCacheSession.TimeToLive = ttlJToken.Value<long>();
            }

            if (jObject.TryGetValue("isSlidingExpiration", out JToken ttlSlidingExpiration))
            {
                cosmosCacheSession.IsSlidingExpiration = ttlSlidingExpiration.Value<bool>();
            }

            return cosmosCacheSession;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            CosmosCacheSession cosmosCacheSession = value as CosmosCacheSession;
            writer.WritePropertyName("id");
            writer.WriteValue(cosmosCacheSession.SessionKey);

            writer.WritePropertyName("content");
            writer.WriteValue(cosmosCacheSession.Content);

            if (cosmosCacheSession.TimeToLive.HasValue)
            {
                writer.WritePropertyName("ttl");
                writer.WriteValue(cosmosCacheSession.TimeToLive.Value);
            }

            if (cosmosCacheSession.IsSlidingExpiration.HasValue)
            {
                writer.WritePropertyName("isSlidingExpiration");
                writer.WriteValue(cosmosCacheSession.IsSlidingExpiration.Value);
            }
        }
    }
}

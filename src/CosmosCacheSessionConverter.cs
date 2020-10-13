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
        private static readonly string ContentAttributeName = "content";
        private static readonly string TtlAttributeName = "ttl";
        private static readonly string SlidingAttributeName = "isSlidingExpiration";
        private static readonly string AbsoluteSlidingExpirationAttributeName = "absoluteSlidingExpiration";
        private static readonly string IdAttributeName = "id";
        private static readonly string PkAttributeName = "partitionKeyDefinition";

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

            if (!jObject.TryGetValue(CosmosCacheSessionConverter.IdAttributeName, out JToken idJToken))
            {
                throw new JsonReaderException("Missing id on Cosmos DB session item.");
            }

            cosmosCacheSession.SessionKey = idJToken.Value<string>();

            if (!jObject.TryGetValue(CosmosCacheSessionConverter.ContentAttributeName, out JToken contentJToken))
            {
                throw new JsonReaderException("Missing id on Cosmos DB session item.");
            }

            cosmosCacheSession.Content = Convert.FromBase64String(contentJToken.Value<string>());

            if (jObject.TryGetValue(CosmosCacheSessionConverter.TtlAttributeName, out JToken ttlJToken))
            {
                cosmosCacheSession.TimeToLive = ttlJToken.Value<long>();
            }

            if (jObject.TryGetValue(CosmosCacheSessionConverter.SlidingAttributeName, out JToken ttlSlidingExpirationJToken))
            {
                cosmosCacheSession.IsSlidingExpiration = ttlSlidingExpirationJToken.Value<bool>();
            }

            if (jObject.TryGetValue(CosmosCacheSessionConverter.AbsoluteSlidingExpirationAttributeName, out JToken absoluteSlidingExpirationJToken))
            {
                cosmosCacheSession.AbsoluteSlidingExpiration = absoluteSlidingExpirationJToken.Value<long>();
            }

            if (jObject.TryGetValue(CosmosCacheSessionConverter.PkAttributeName, out JToken pkDefinitionJToken))
            {
                cosmosCacheSession.PartitionKeyAttribute = pkDefinitionJToken.Value<string>();
            }

            return cosmosCacheSession;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            CosmosCacheSession cosmosCacheSession = value as CosmosCacheSession;

            writer.WriteStartObject();

            writer.WritePropertyName(CosmosCacheSessionConverter.IdAttributeName);
            writer.WriteValue(cosmosCacheSession.SessionKey);

            writer.WritePropertyName(CosmosCacheSessionConverter.ContentAttributeName);
            writer.WriteValue(Convert.ToBase64String(cosmosCacheSession.Content));

            if (cosmosCacheSession.TimeToLive.HasValue)
            {
                writer.WritePropertyName(CosmosCacheSessionConverter.TtlAttributeName);
                writer.WriteValue(cosmosCacheSession.TimeToLive.Value);
            }

            if (cosmosCacheSession.IsSlidingExpiration.HasValue)
            {
                writer.WritePropertyName(CosmosCacheSessionConverter.SlidingAttributeName);
                writer.WriteValue(cosmosCacheSession.IsSlidingExpiration.Value);
            }

            if (cosmosCacheSession.AbsoluteSlidingExpiration.HasValue)
            {
                writer.WritePropertyName(CosmosCacheSessionConverter.AbsoluteSlidingExpirationAttributeName);
                writer.WriteValue(cosmosCacheSession.AbsoluteSlidingExpiration.Value);
            }

            if (!string.IsNullOrWhiteSpace(cosmosCacheSession.PartitionKeyAttribute)
                && !CosmosCacheSessionConverter.IdAttributeName.Equals(cosmosCacheSession.PartitionKeyAttribute, StringComparison.OrdinalIgnoreCase))
            {
                writer.WritePropertyName(cosmosCacheSession.PartitionKeyAttribute);
                writer.WriteValue(cosmosCacheSession.SessionKey);
            }

            writer.WriteEndObject();
        }
    }
}

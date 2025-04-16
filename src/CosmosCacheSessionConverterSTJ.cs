//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class CosmosCacheSessionConverterSTJ : JsonConverter<CosmosCacheSession>
    {
        private const string ContentAttributeName = "content";
        private const string TtlAttributeName = "ttl";
        private const string SlidingAttributeName = "isSlidingExpiration";
        private const string AbsoluteSlidingExpirationAttributeName = "absoluteSlidingExpiration";
        private const string IdAttributeName = "id";
        private const string PkAttributeName = "partitionKeyDefinition";

        public override CosmosCacheSession Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            CosmosCacheSession cosmosCacheSession = new CosmosCacheSession();
            string content = null;
            bool hasSessionKey = false;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name");
                }

                string propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case IdAttributeName:
                        string sessionKey = reader.GetString();
                        hasSessionKey = true;
                        cosmosCacheSession.SessionKey = sessionKey;
                        break;

                    case ContentAttributeName:
                        content = reader.GetString();
                        cosmosCacheSession.Content = Convert.FromBase64String(content);
                        break;

                    case TtlAttributeName:
                        cosmosCacheSession.TimeToLive = reader.GetInt64();
                        break;

                    case SlidingAttributeName:
                        cosmosCacheSession.IsSlidingExpiration = reader.GetBoolean();
                        break;

                    case AbsoluteSlidingExpirationAttributeName:
                        cosmosCacheSession.AbsoluteSlidingExpiration = reader.GetInt64();
                        break;

                    case PkAttributeName:
                        cosmosCacheSession.PartitionKeyAttribute = reader.GetString();
                        break;
                }
            }

            if (!hasSessionKey)
            {
                throw new JsonException("Missing 'id' on Cosmos DB session item.");
            }

            if (content == null)
            {
                throw new JsonException("Missing 'content' on Cosmos DB session item.");
            }

            return cosmosCacheSession;
        }

        public override void Write(Utf8JsonWriter writer, CosmosCacheSession value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(IdAttributeName, value.SessionKey);
            writer.WriteString(ContentAttributeName, Convert.ToBase64String(value.Content));

            if (value.TimeToLive.HasValue)
            {
                writer.WriteNumber(TtlAttributeName, value.TimeToLive.Value);
            }

            if (value.IsSlidingExpiration.HasValue)
            {
                writer.WriteBoolean(SlidingAttributeName, value.IsSlidingExpiration.Value);
            }

            if (value.AbsoluteSlidingExpiration.HasValue)
            {
                writer.WriteNumber(AbsoluteSlidingExpirationAttributeName, value.AbsoluteSlidingExpiration.Value);
            }

            if (!string.IsNullOrWhiteSpace(value.PartitionKeyAttribute)
                && !IdAttributeName.Equals(value.PartitionKeyAttribute, StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteString(value.PartitionKeyAttribute, value.SessionKey);
            }

            writer.WriteEndObject();
        }
    }
}
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Caching.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;
    using Moq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Xunit;

    public class CosmosSessionSerializationSTJTests
    {
        [Fact]
        public void ValidatesNullTtlDoesNotSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            string serialized = JsonSerializer.Serialize(existingSession);
            Assert.False(serialized.Contains("\"ttl\""), "Session without expiration should not include ttl property.");
            Assert.False(serialized.Contains("\"absoluteSlidingExpiration\""), "Session without expiration should not include absoluteSlidingExpiration property.");
            Assert.False(serialized.Contains("\"isSlidingExpiration\""), "Session without expiration should not include isSlidingExpiration property.");
        }

        [Fact]
        public void ValidatesCustomPartitionKeyCreatesProperty()
        {
            const string pkProperty = "notTheId";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.PartitionKeyAttribute = pkProperty;
            string serialized = JsonSerializer.Serialize(existingSession);
            Assert.True(serialized.Contains($"\"{pkProperty}\""), "Missing custom partition key.");
        }

        [Fact]
        public void ValidatesAbsoluteSlidingExpirationDoesSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.AbsoluteSlidingExpiration = 10;
            string serialized = JsonSerializer.Serialize(existingSession);
            Assert.True(serialized.Contains("\"absoluteSlidingExpiration\""), "Session with absolute sliding expiration should include absoluteSlidingExpiration property.");
        }

        [Fact]
        public void ValidatesIsSlidingExpirationDoesSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            string serialized = JsonSerializer.Serialize(existingSession);
            Assert.True(serialized.Contains("\"isSlidingExpiration\""), "Session with sliding expiration should include isSlidingExpiration property.");
        }

        [Fact]
        public void ValidatesContract()
        {
            const string expectedContract = "{\"id\":\"key\",\"content\":\"AQ==\",\"ttl\":5,\"isSlidingExpiration\":true,\"absoluteSlidingExpiration\":10}";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.IsSlidingExpiration = true;
            existingSession.TimeToLive = 5;
            existingSession.AbsoluteSlidingExpiration = 10;
            existingSession.Content = new byte[1] { 1 };
            string serialized = JsonSerializer.Serialize(existingSession);
            Assert.Equal(expectedContract, serialized);
        }

        [Fact]
        public void MissingRequiredProperties()
        {
            const string withoutId = "{\"content\":\"AQ==\",\"ttl\":5,\"isSlidingExpiration\":true,\"absoluteSlidingExpiration\":10}";
            JsonException withoutIdException = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CosmosCacheSession>(withoutId));
            Assert.Contains("Missing 'id'", withoutIdException.Message);

            const string withoutContent = "{\"id\":\"1\", \"ttl\":5,\"isSlidingExpiration\":true,\"absoluteSlidingExpiration\":10}";
            JsonException withoutContentException = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CosmosCacheSession>(withoutContent));
            Assert.Contains("Missing 'content'", withoutContentException.Message);
        }

        [Fact]
        public void Success()
        {
            const string content = "{\"id\":\"someId\",\"content\":\"AQ==\",\"ttl\":5,\"isSlidingExpiration\":true,\"absoluteSlidingExpiration\":10}";
            CosmosCacheSession session = JsonSerializer.Deserialize<CosmosCacheSession>(content);
            Assert.NotNull(session);
            Assert.Equal("someId", session.SessionKey);
            Assert.Equal(5, session.TimeToLive);
            Assert.Equal(10, session.AbsoluteSlidingExpiration);
            Assert.True(session.IsSlidingExpiration);
        }
    }
}
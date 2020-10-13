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
    using Newtonsoft.Json;
    using Xunit;

    public class CosmosSessionSerializationTests
    {
        [Fact]
        public void ValidatesNullTtlDoesNotSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            string serialized = JsonConvert.SerializeObject(existingSession);
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
            string serialized = JsonConvert.SerializeObject(existingSession);
            Assert.True(serialized.Contains($"\"{pkProperty}\""), "Missing custom partition key.");
        }

        [Fact]
        public void ValidatesAbsoluteSlidingExpirationDoesSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.AbsoluteSlidingExpiration = 10;
            string serialized = JsonConvert.SerializeObject(existingSession);
            Assert.True(serialized.Contains("\"absoluteSlidingExpiration\""), "Session with absolute sliding expiration should include absoluteSlidingExpiration property.");
        }

        [Fact]
        public void ValidatesIsSlidingExpirationDoesSerializeProperty()
        {
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            string serialized = JsonConvert.SerializeObject(existingSession);
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
            string serialized = JsonConvert.SerializeObject(existingSession);
            Assert.Equal(expectedContract , serialized);
        }
    }
}
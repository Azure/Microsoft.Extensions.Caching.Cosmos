//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Caching.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using Xunit;

    public class CosmosCacheEmulatorTests : IDisposable
    {
        private const string databaseName = "state";
        private readonly CosmosClient testClient;

        public CosmosCacheEmulatorTests()
        {
            this.testClient = new CosmosClient(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
        }

        public void Dispose()
        {
            this.testClient.GetDatabase(CosmosCacheEmulatorTests.databaseName).DeleteAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task InitializeContainerIfNotExists()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                DefaultTimeToLiveInMs = ttl
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, new byte[0], cacheOptions);

            // Verify that container has been created

            ContainerResponse response = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadContainerAsync();
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ttl, response.Resource.DefaultTimeToLive);
            Assert.True(response.Resource.IndexingPolicy.ExcludedPaths.Any(e => e.Path.Equals("/*")));

            int? throughputContainer = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadThroughputAsync();

            Assert.Equal(throughput, throughputContainer);
        }

        [Fact]
        public async Task InitializeContainerIfNotExists_CustomPartitionKey()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                DefaultTimeToLiveInMs = ttl,
                ContainerPartitionKeyAttribute = partitionKeyAttribute,
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, new byte[0], cacheOptions);

            // Verify that container has been created

            ContainerResponse response = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadContainerAsync();
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ttl, response.Resource.DefaultTimeToLive);
            Assert.True(response.Resource.IndexingPolicy.ExcludedPaths.Any(e => e.Path.Equals("/*")));
            Assert.Equal($"/{partitionKeyAttribute}", response.Resource.PartitionKeyPath);

            int? throughputContainer = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadThroughputAsync();

            Assert.Equal(throughput, throughputContainer);
        }

        [Fact]
        public async Task StoreSessionData()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            byte[] data = new byte[4] { 1, 2, 3, 4 };
            await cache.SetAsync(sessionId, data, cacheOptions);

            // Verify that container has been created

            CosmosCacheSession storedSession = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));
            Assert.Equal(sessionId, storedSession.SessionKey);
            Assert.Equal(data, storedSession.Content);
        }

        [Fact]
        public async Task StoreSessionData_CustomPartitionKey()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                ContainerPartitionKeyAttribute = partitionKeyAttribute,
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            byte[] data = new byte[4] { 1, 2, 3, 4 };
            await cache.SetAsync(sessionId, data, cacheOptions);

            // Verify that container has been created

            CosmosCacheSession storedSession = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));
            Assert.Equal(sessionId, storedSession.SessionKey);
            Assert.Equal(data, storedSession.Content);

            ItemResponse<dynamic> dynamicSession = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<dynamic>(sessionId, new PartitionKey(sessionId));
            Assert.NotNull(dynamicSession.Resource.notTheId);
            Assert.Equal(sessionId, (string)dynamicSession.Resource.notTheId);
        }

        [Fact]
        public async Task GetSessionData()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, data, cacheOptions);

            Assert.Equal(data, await cache.GetAsync(sessionId));
        }

        [Fact]
        public async Task GetSessionData_CustomPartitionKey()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                ContainerPartitionKeyAttribute = partitionKeyAttribute,
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, data, cacheOptions);

            Assert.Equal(data, await cache.GetAsync(sessionId));
        }

        [Fact]
        public async Task GetSessionData_WhenNotExists()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            Assert.Null(await cache.GetAsync(sessionId));
        }

        [Fact]
        public async Task GetSessionData_WhenNotExists_CustomPartitionKey()
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                ContainerPartitionKeyAttribute = partitionKeyAttribute,
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            Assert.Null(await cache.GetAsync(sessionId));
        }

        private class CosmosCacheSession
        {
            [JsonProperty("id")]
            public string SessionKey { get; set; }

            [JsonProperty("content")]
            public byte[] Content { get; set; }

            [JsonProperty("ttl")]
            public long? TimeToLive { get; set; }
        }
    }
}
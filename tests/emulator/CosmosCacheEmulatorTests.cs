//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
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
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            const string sessionId = "sessionId";
            const int ttl = 2000;
            const int ttlInSeconds = ttl / 1000;
            const int throughput = 2000;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                DefaultTimeToLiveInMs = ttl,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttlInSeconds);
            await cache.SetAsync(sessionId, new byte[0], cacheOptions);

            // Verify that container has been created

            ContainerResponse response = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadContainerAsync();
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ttlInSeconds, response.Resource.DefaultTimeToLive);
            Assert.True(response.Resource.IndexingPolicy.ExcludedPaths.Any(e => e.Path.Equals("/*")));

            int? throughputContainer = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadThroughputAsync();

            Assert.Equal(throughput, throughputContainer);

            Assert.Equal(4, diagnosticsSink.CapturedDiagnostics.Count);
            foreach (CosmosDiagnostics diagnostics in diagnosticsSink.CapturedDiagnostics)
            {
                Assert.NotNull(diagnostics?.ToString());
            }
        }

        [Fact]
        public async Task InitializeContainerIfNotExists_CustomPartitionKey()
        {
            const string sessionId = "sessionId";
            const int ttl = 2000;
            const int ttlInSeconds = ttl / 1000;
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
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttlInSeconds);
            await cache.SetAsync(sessionId, new byte[0], cacheOptions);

            // Verify that container has been created

            ContainerResponse response = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadContainerAsync();
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(ttlInSeconds, response.Resource.DefaultTimeToLive);
            Assert.True(response.Resource.IndexingPolicy.ExcludedPaths.Any(e => e.Path.Equals("/*")));
            Assert.Equal($"/{partitionKeyAttribute}", response.Resource.PartitionKeyPath);

            int? throughputContainer = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadThroughputAsync();

            Assert.Equal(throughput, throughputContainer);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task StoreSessionData(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task StoreSessionData_CustomPartitionKey(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetSessionData(bool useSystemTextJson)
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, data, cacheOptions);

            Assert.Equal(data, await cache.GetAsync(sessionId));

            Assert.Equal(6, diagnosticsSink.CapturedDiagnostics.Count);
            foreach (CosmosDiagnostics diagnostics in diagnosticsSink.CapturedDiagnostics)
            {
                Assert.NotNull(diagnostics?.ToString());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RemoveSessionData(bool useSystemTextJson)
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.SetAsync(sessionId, data, cacheOptions);

            await cache.RemoveAsync(sessionId);

            CosmosException exception = await Assert.ThrowsAsync<CosmosException>(() => this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<dynamic>(sessionId, new PartitionKey(sessionId)));
            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);

            Assert.Equal(5, diagnosticsSink.CapturedDiagnostics.Count);
            foreach (CosmosDiagnostics diagnostics in diagnosticsSink.CapturedDiagnostics)
            {
                Assert.NotNull(diagnostics?.ToString());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetSessionData_CustomPartitionKey(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RemoveSessionData_CustomPartitionKey(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 1400;
            const int throughput = 2000;
            byte[] data = new byte[1] { 1 };
            const string partitionKeyAttribute = "notTheId";

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

            await cache.RemoveAsync(sessionId);

            CosmosException exception = await Assert.ThrowsAsync<CosmosException>(() => this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<dynamic>(sessionId, new PartitionKey(sessionId)));
            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        }

        [Fact]
        public async Task RemoveSessionData_WhenNotExists()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

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
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            await cache.RemoveAsync(sessionId);

            Assert.Equal(4, diagnosticsSink.CapturedDiagnostics.Count);
            foreach (CosmosDiagnostics diagnostics in diagnosticsSink.CapturedDiagnostics)
            {
                Assert.NotNull(diagnostics?.ToString());
            }
        }

        [Fact]
        public async Task GetSessionData_WhenNotExists()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

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
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            Assert.Null(await cache.GetAsync(sessionId));

            Assert.Equal(4, diagnosticsSink.CapturedDiagnostics.Count);
            foreach (CosmosDiagnostics diagnostics in diagnosticsSink.CapturedDiagnostics)
            {
                Assert.NotNull(diagnostics?.ToString());
            }
        }

        [Fact]
        public async Task RemoveSessionData_WhenNotExists_CustomPartitionKey()
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
            await cache.RemoveAsync(sessionId);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SlidingAndAbsoluteExpiration(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 10;
            const int absoluteTtl = 15;
            const int throughput = 400;

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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
            cacheOptions.AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(absoluteTtl);
            byte[] data = new byte[4] { 1, 2, 3, 4 };
            await cache.SetAsync(sessionId, data, cacheOptions);

            // Verify that container has been created

            CosmosCacheSession storedSession = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));
            Assert.Equal(ttl, storedSession.TimeToLive);

            await Task.Delay(8000); // Wait

            await cache.GetAsync(sessionId);

            storedSession = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            // Since the absolute expiration is closer than the sliding value, the TTL should be lower
            Assert.True(storedSession.TimeToLive < ttl);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetResetsSlidingExpirationTimestamp(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 600;
            const int throughput = 400;
            byte[] data = new byte[4] { 1, 2, 3, 4 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

            CosmosCacheSession before = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            await Task.Delay(2000); // _ts has 1-second granularity; wait so a bump is observable.

            Assert.Equal(data, await cache.GetAsync(sessionId));

            CosmosCacheSession after = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            // Patching /ttl (to its current value here) must update _ts, resetting the sliding window,
            // while leaving the ttl value and the content untouched.
            Assert.True(after.Timestamp > before.Timestamp, $"expected _ts to advance (before={before.Timestamp}, after={after.Timestamp})");
            Assert.Equal(ttl, after.TimeToLive);
            Assert.Equal(data, after.Content);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RefreshResetsSlidingExpirationTimestamp(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int ttl = 600;
            const int throughput = 400;
            byte[] data = new byte[4] { 1, 2, 3, 4 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

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

            CosmosCacheSession before = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            await Task.Delay(2000); // _ts has 1-second granularity; wait so a bump is observable.

            await cache.RefreshAsync(sessionId);

            CosmosCacheSession after = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            // Refresh behaves like Get: patch /ttl to reset the window (bump _ts), content untouched.
            Assert.True(after.Timestamp > before.Timestamp, $"expected _ts to advance (before={before.Timestamp}, after={after.Timestamp})");
            Assert.Equal(ttl, after.TimeToLive);
            Assert.Equal(data, after.Content);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AbsoluteExpirationIsNotSlidOnGetOrRefresh(bool useSystemTextJson)
        {
            const string sessionId = "sessionId";
            const int absoluteTtl = 600;
            const int throughput = 400;
            byte[] data = new byte[4] { 1, 2, 3, 4 };

            CosmosClientBuilder builder = new CosmosClientBuilder(ConfigurationManager.AppSettings["Endpoint"], ConfigurationManager.AppSettings["MasterKey"]);
            if (useSystemTextJson)
            {
                builder.WithSystemTextJsonSerializerOptions(new System.Text.Json.JsonSerializerOptions());
            }

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){
                ContainerName = "session",
                DatabaseName = CosmosCacheEmulatorTests.databaseName,
                ContainerThroughput = throughput,
                CreateIfNotExists = true,
                ClientBuilder = builder
            });

            CosmosCache cache = new CosmosCache(options);
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(absoluteTtl);
            await cache.SetAsync(sessionId, data, cacheOptions);

            CosmosCacheSession before = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            await Task.Delay(2000);

            await cache.GetAsync(sessionId);
            await cache.RefreshAsync(sessionId);

            CosmosCacheSession after = await this.testClient.GetContainer(CosmosCacheEmulatorTests.databaseName, "session").ReadItemAsync<CosmosCacheSession>(sessionId, new PartitionKey(sessionId));

            // An absolute-only item is not sliding, so Get/Refresh must not write to it (no _ts bump, no over-extension).
            Assert.Equal(before.Timestamp, after.Timestamp);
            Assert.Equal(data, after.Content);
        }

        private class CosmosCacheSession
        {
            [JsonProperty("id")]
            public string SessionKey { get; set; }

            [JsonProperty("content")]
            public byte[] Content { get; set; }

            [JsonProperty("ttl")]
            public long? TimeToLive { get; set; }

            [JsonProperty("_ts")]
            public long Timestamp { get; set; }
        }

        private class DiagnosticsSink 
        {
            private List<CosmosDiagnostics> capturedDiagnostics = new List<CosmosDiagnostics>();

            public IReadOnlyList<CosmosDiagnostics> CapturedDiagnostics => this.capturedDiagnostics.AsReadOnly();

            public void CaptureDiagnostics(CosmosDiagnostics diagnostics)
            {
                this.capturedDiagnostics.Add(diagnostics);
            }
        }
    }
}
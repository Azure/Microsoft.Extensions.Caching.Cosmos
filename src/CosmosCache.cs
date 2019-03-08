namespace Microsoft.Extensions.Caching.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Distributed cache implementation over Azure Cosmos DB
    /// </summary>
    public class CosmosCache : IDistributedCache, IDisposable
    {
        private const string UseUserAgentSuffix = "Microsoft.Extensions.Caching.Cosmos";
        private const string ContainerPartitionKeyPath = "/id";
        private const int DefaultTimeToLive = 60000;

        private CosmosClient cosmosClient;
        private CosmosContainer cosmosContainer;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        private bool initializedClient;

        private readonly CosmosCacheOptions options;

        public CosmosCache(IOptions<CosmosCacheOptions> optionsAccessor)
        {
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            if (string.IsNullOrEmpty(optionsAccessor.Value.DatabaseName))
            {
                throw new ArgumentNullException(nameof(optionsAccessor.Value.DatabaseName));
            }

            if (string.IsNullOrEmpty(optionsAccessor.Value.ContainerName))
            {
                throw new ArgumentNullException(nameof(optionsAccessor.Value.ContainerName));
            }

            if (optionsAccessor.Value.Configuration == null && optionsAccessor.Value.CosmosClient == null)
            {
                throw new ArgumentNullException("You need to specify either a CosmosConfiguration or an existing CosmosClient in the CosmosCacheOptions.");
            }

            this.options = optionsAccessor.Value;
        }

        public void Dispose()
        {
            if (this.initializedClient && this.cosmosClient != null)
            {
                this.cosmosClient.Dispose();
            }
        }

        public byte[] Get(string key)
        {
            return GetAsync(key).GetAwaiter().GetResult();
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            await this.ConnectAsync();

            CosmosItemResponse<CosmosCacheSession> cosmosCacheSessionResponse = await this.cosmosContainer.Items.ReadItemAsync<CosmosCacheSession>(
                partitionKey: key,
                id: key,
                requestOptions: null,
                cancellationToken: token).ConfigureAwait(false);

            if (cosmosCacheSessionResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            try
            {
                await this.cosmosContainer.Items.ReplaceItemAsync(
                        partitionKey: key,
                        id: key,
                        item: cosmosCacheSessionResponse.Resource,
                        requestOptions: new CosmosItemRequestOptions()
                        {
                            AccessCondition = new AccessCondition()
                            {
                                Type = AccessConditionType.IfMatch,
                                Condition = cosmosCacheSessionResponse.ETag
                            }
                        },
                        cancellationToken: token).ConfigureAwait(false);
            }
            catch (CosmosException cosmosException) 
                when (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // Race condition on replace, we need to get the latest version of the item
                return await this.GetAsync(key, token).ConfigureAwait(false);
            }

            return cosmosCacheSessionResponse.Resource.Content;
        }

        public void Refresh(string key)
        {
            Get(key);
        }

        public Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            return GetAsync(key, token);
        }

        public void Remove(string key)
        {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            await this.ConnectAsync();

            await this.cosmosContainer.Items.DeleteItemAsync<CosmosCacheSession>(
                partitionKey: key,
                id: key,
                requestOptions: null,
                cancellationToken: token).ConfigureAwait(false);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            this.SetAsync(
                key,
                value,
                options).GetAwaiter().GetResult();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            await this.ConnectAsync();

            await this.cosmosContainer.Items.UpsertItemAsync(
                partitionKey: key,
                item: CosmosCache.BuildCosmosCacheSession(
                    key,
                    value,
                    options),
                requestOptions: null,
                cancellationToken: token).ConfigureAwait(false);
        }

        private async Task ConnectAsync(CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (this.cosmosContainer != null)
            {
                return;
            }

            await this.connectionLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (this.cosmosContainer == null)
                {
                    this.cosmosContainer = await this.CosmosContainerInitializeAsync();
                }
            }
            finally
            {
                this.connectionLock.Release();
            }
        }

        private async Task<CosmosContainer> CosmosContainerInitializeAsync()
        {
            this.initializedClient = this.options.CosmosClient == null;
            this.cosmosClient = this.options.CosmosClient ?? new CosmosClient(this.options.Configuration.UseUserAgentSuffix(CosmosCache.UseUserAgentSuffix));
            if (this.options.CreateIfNotExists)
            {
                await this.cosmosClient.Databases.CreateDatabaseIfNotExistsAsync(this.options.DatabaseName).ConfigureAwait(false);

                int defaultTimeToLive = options.DefaultTimeToLiveInMs.HasValue
                    && options.DefaultTimeToLiveInMs.Value > 0 ? options.DefaultTimeToLiveInMs.Value : CosmosCache.DefaultTimeToLive;

                // Container is optimized as Key-Value store excluding all properties
                await this.cosmosClient.Databases[this.options.DatabaseName].Containers.CreateContainerIfNotExistsAsync(
                    new CosmosContainerSettings(
                        this.options.ContainerName,
                        CosmosCache.ContainerPartitionKeyPath)
                    {
                        IndexingPolicy = new IndexingPolicy()
                        {
                            IndexingMode = IndexingMode.Consistent,
                            ExcludedPaths = new Collection<ExcludedPath>()
                            {
                                new ExcludedPath() { Path = "/*" }
                            },
                            IncludedPaths = new Collection<IncludedPath>()
                        },
                        DefaultTimeToLive = TimeSpan.FromMilliseconds(defaultTimeToLive)
                    },
                    throughput: this.options.ContainerThroughput
                    ).ConfigureAwait(false);
            }
            else
            {
                CosmosContainerResponse existingContainer = await this.cosmosClient.Databases[this.options.DatabaseName].Containers[this.options.ContainerName].ReadAsync().ConfigureAwait(false);
                if (existingContainer.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException($"Cannot find an existing container named {this.options.ContainerName} within database {this.options.DatabaseName}");
                }
            }

            return this.cosmosClient.Databases[this.options.DatabaseName].Containers[this.options.ContainerName];
        }

        private static CosmosCacheSession BuildCosmosCacheSession(string key, byte[] content, DistributedCacheEntryOptions options, DateTimeOffset? creationTime = null)
        {
            if (!creationTime.HasValue)
            {
                creationTime = DateTimeOffset.UtcNow;
            }

            DateTimeOffset? absoluteExpiration = CosmosCache.GetAbsoluteExpiration(creationTime.Value, options);

            long? timeToLive = CosmosCache.GetExpirationInSeconds(creationTime.Value, absoluteExpiration, options);

            return new CosmosCacheSession()
            {
                SessionKey = key,
                Content = content,
                TimeToLive = timeToLive
            };
        }

        private static long? GetExpirationInSeconds(DateTimeOffset creationTime, DateTimeOffset? absoluteExpiration, DistributedCacheEntryOptions options)
        {
            if (absoluteExpiration.HasValue && options.SlidingExpiration.HasValue)
            {
                return (long)Math.Min(
                    (absoluteExpiration.Value - creationTime).TotalSeconds,
                    options.SlidingExpiration.Value.TotalSeconds);
            }
            else if (absoluteExpiration.HasValue)
            {
                return (long)(absoluteExpiration.Value - creationTime).TotalSeconds;
            }
            else if (options.SlidingExpiration.HasValue)
            {
                return (long)options.SlidingExpiration.Value.TotalSeconds;
            }

            return null;
        }

        private static DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset creationTime, DistributedCacheEntryOptions options)
        {
            if (options.AbsoluteExpiration.HasValue && options.AbsoluteExpiration <= creationTime)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(DistributedCacheEntryOptions.AbsoluteExpiration),
                    options.AbsoluteExpiration.Value,
                    "The absolute expiration value must be in the future.");
            }
            var absoluteExpiration = options.AbsoluteExpiration;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = creationTime + options.AbsoluteExpirationRelativeToNow;
            }

            return absoluteExpiration;
        }
    }
}

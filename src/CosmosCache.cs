//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Distributed cache implementation over Microsoft Azure Cosmos DB.
    /// </summary>
    public class CosmosCache : IDistributedCache, IDisposable
    {
        private const string UseUserAgentSuffix = "Microsoft.Extensions.Caching.Cosmos";
        private const string ContainerPartitionKeyPath = "/id";
        private const int DefaultTimeToLive = -1;
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        private readonly IDisposable monitorListener;
        private CosmosCacheOptions options;
        private CosmosClient cosmosClient;
        private Container cosmosContainer;
        private bool initializedClient;
        private bool isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosCache"/> class.
        /// </summary>
        /// <param name="optionsAccessor">Options accessor.</param>
        public CosmosCache(IOptions<CosmosCacheOptions> optionsAccessor)
        {
            this.Initialize(optionsAccessor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosCache"/> class.
        /// </summary>
        /// <remarks>
        /// Using the <see cref="IOptionsMonitor{T}"/> would make the internal client reference to be updated if any of the options change.
        /// </remarks>
        /// <param name="optionsMonitor">Options monitor.</param>
        public CosmosCache(IOptionsMonitor<CosmosCacheOptions> optionsMonitor)
        {
            if (optionsMonitor == null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            this.Initialize(optionsMonitor.CurrentValue);

            this.monitorListener = optionsMonitor.OnChange(this.OnOptionsChange);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.initializedClient && this.cosmosClient != null)
            {
                this.cosmosClient.Dispose();
            }

            this.monitorListener?.Dispose();

            this.isDisposed = true;
        }

        /// <inheritdoc/>
        public byte[] Get(string key)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            return this.GetAsync(key).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            await this.ConnectAsync(token).ConfigureAwait(false);

            ItemResponse<CosmosCacheSession> cosmosCacheSessionResponse;
            try
            {
                cosmosCacheSessionResponse = await this.cosmosContainer.ReadItemAsync<CosmosCacheSession>(
                    partitionKey: new PartitionKey(key),
                    id: key,
                    requestOptions: null,
                    cancellationToken: token).ConfigureAwait(false);

                this.options.DiagnosticsHandler?.Invoke(cosmosCacheSessionResponse.Diagnostics);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                this.options.DiagnosticsHandler?.Invoke(ex.Diagnostics);
                return null;
            }

            // If using sliding expiration then replace item with itself in order to reset the ttl in Cosmos
            if (cosmosCacheSessionResponse.Resource.IsSlidingExpiration.GetValueOrDefault())
            {
                try
                {
                    if (cosmosCacheSessionResponse.Resource.AbsoluteSlidingExpiration.GetValueOrDefault() > 0)
                    {
                        long ttl = cosmosCacheSessionResponse.Resource.TimeToLive.Value;
                        DateTimeOffset absoluteExpiration = DateTimeOffset.FromUnixTimeSeconds(cosmosCacheSessionResponse.Resource.AbsoluteSlidingExpiration.GetValueOrDefault());
                        if (absoluteExpiration < DateTimeOffset.UtcNow)
                        {
                            cosmosCacheSessionResponse.Resource.TimeToLive = 0;
                        }
                        else
                        {
                            double pendingSeconds = (absoluteExpiration - DateTimeOffset.UtcNow).TotalSeconds;
                            if (pendingSeconds < ttl)
                            {
                                cosmosCacheSessionResponse.Resource.TimeToLive = (long)pendingSeconds;
                            }
                        }
                    }

                    cosmosCacheSessionResponse.Resource.PartitionKeyAttribute = this.options.ContainerPartitionKeyAttribute;
                    ItemResponse<CosmosCacheSession> replaceCacheSessionResponse = await this.cosmosContainer.ReplaceItemAsync(
                            partitionKey: new PartitionKey(key),
                            id: key,
                            item: cosmosCacheSessionResponse.Resource,
                            requestOptions: new ItemRequestOptions()
                            {
                                IfMatchEtag = cosmosCacheSessionResponse.ETag,
                                EnableContentResponseOnWrite = false,
                            },
                            cancellationToken: token).ConfigureAwait(false);

                    this.options.DiagnosticsHandler?.Invoke(replaceCacheSessionResponse.Diagnostics);
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    this.options.DiagnosticsHandler?.Invoke(cosmosException.Diagnostics);
                    if (this.options.RetrySlidingExpirationUpdates)
                    {
                        // Race condition on replace, we need to get the latest version of the item
                        return await this.GetAsync(key, token).ConfigureAwait(false);
                    }
                }
            }

            return cosmosCacheSessionResponse.Resource.Content;
        }

        /// <inheritdoc/>
        public void Refresh(string key)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            this.RefreshAsync(key).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        /// <inheritdoc/>
        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            await this.ConnectAsync(token).ConfigureAwait(false);

            ItemResponse<CosmosCacheSession> cosmosCacheSessionResponse;
            try
            {
                cosmosCacheSessionResponse = await this.cosmosContainer.ReadItemAsync<CosmosCacheSession>(
                    partitionKey: new PartitionKey(key),
                    id: key,
                    requestOptions: null,
                    cancellationToken: token).ConfigureAwait(false);

                this.options.DiagnosticsHandler?.Invoke(cosmosCacheSessionResponse.Diagnostics);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                this.options.DiagnosticsHandler?.Invoke(ex.Diagnostics);
                return;
            }

            if (cosmosCacheSessionResponse.Resource.IsSlidingExpiration.GetValueOrDefault())
            {
                try
                {
                    cosmosCacheSessionResponse.Resource.PartitionKeyAttribute = this.options.ContainerPartitionKeyAttribute;
                    ItemResponse<CosmosCacheSession> replaceCacheSessionResponse = await this.cosmosContainer.ReplaceItemAsync(
                            partitionKey: new PartitionKey(key),
                            id: key,
                            item: cosmosCacheSessionResponse.Resource,
                            requestOptions: new ItemRequestOptions()
                            {
                                IfMatchEtag = cosmosCacheSessionResponse.ETag,
                                EnableContentResponseOnWrite = false,
                            },
                            cancellationToken: token).ConfigureAwait(false);

                    this.options.DiagnosticsHandler?.Invoke(replaceCacheSessionResponse.Diagnostics);
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // Race condition on replace, we need do not need to refresh it
                    this.options.DiagnosticsHandler?.Invoke(cosmosException.Diagnostics);
                }
            }
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            this.RemoveAsync(key).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        /// <inheritdoc/>
        public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            await this.ConnectAsync(token).ConfigureAwait(false);
            try
            {
                ItemResponse<CosmosCacheSession> deleteCacheSessionResponse = await this.cosmosContainer.DeleteItemAsync<CosmosCacheSession>(
                    partitionKey: new PartitionKey(key),
                    id: key,
                    requestOptions: new ItemRequestOptions()
                    {
                        EnableContentResponseOnWrite = false,
                    },
                    cancellationToken: token).ConfigureAwait(false);

                this.options.DiagnosticsHandler?.Invoke(deleteCacheSessionResponse.Diagnostics);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // do nothing
                this.options.DiagnosticsHandler?.Invoke(ex.Diagnostics);
            }
        }

        /// <inheritdoc/>
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            this.SetAsync(key, value, options).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        /// <inheritdoc/>
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

            await this.ConnectAsync(token).ConfigureAwait(false);

            ItemResponse<CosmosCacheSession> setCacheSessionResponse = await this.cosmosContainer.UpsertItemAsync(
                partitionKey: new PartitionKey(key),
                item: CosmosCache.BuildCosmosCacheSession(
                    key,
                    value,
                    options,
                    this.options),
                requestOptions: new ItemRequestOptions()
                {
                    EnableContentResponseOnWrite = false,
                },
                cancellationToken: token).ConfigureAwait(false);

            this.options.DiagnosticsHandler?.Invoke(setCacheSessionResponse.Diagnostics);
        }

        private static CosmosCacheSession BuildCosmosCacheSession(string key, byte[] content, DistributedCacheEntryOptions options, CosmosCacheOptions cosmosCacheOptions)
        {
            DateTimeOffset creationTime = DateTimeOffset.UtcNow;

            DateTimeOffset? absoluteExpiration = CosmosCache.GetAbsoluteExpiration(creationTime, options);

            long? timeToLive = CosmosCache.GetExpirationInSeconds(creationTime, absoluteExpiration, options);

            bool hasSlidingExpiration = timeToLive.HasValue && options.SlidingExpiration.HasValue;

            long? absoluteSlidingExpiration = null;

            if (hasSlidingExpiration && absoluteExpiration.HasValue)
            {
                absoluteSlidingExpiration = absoluteExpiration.Value.ToUnixTimeSeconds();
            }

            return new CosmosCacheSession()
            {
                SessionKey = key,
                Content = content,
                TimeToLive = timeToLive,
                IsSlidingExpiration = hasSlidingExpiration,
                AbsoluteSlidingExpiration = absoluteSlidingExpiration,
                PartitionKeyAttribute = cosmosCacheOptions.ContainerPartitionKeyAttribute,
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

            DateTimeOffset? absoluteExpiration = options.AbsoluteExpiration;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = creationTime + options.AbsoluteExpirationRelativeToNow;
            }

            return absoluteExpiration;
        }

        private void Initialize(IOptions<CosmosCacheOptions> optionsAccessor)
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

            if (optionsAccessor.Value.ClientBuilder == null && optionsAccessor.Value.CosmosClient == null)
            {
                throw new ArgumentNullException("You need to specify either a CosmosConfiguration or an existing CosmosClient in the CosmosCacheOptions.");
            }

            this.options = optionsAccessor.Value;
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
                    this.cosmosContainer = await this.CosmosContainerInitializeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                this.connectionLock.Release();
            }
        }

        private void OnOptionsChange(CosmosCacheOptions options)
        {
            // Did we create our own internal client? If so, we need to dispose it.
            if (this.initializedClient && this.cosmosClient != null)
            {
                // In case this becomes an issue with concurrent access to the client, we can see if ReaderWriterLockSlim can be leveraged.
                this.cosmosClient.Dispose();
            }

            this.options = options;

            // Force re-initialization on the next Connect
            this.cosmosContainer = null;
        }

        private async Task<Container> CosmosContainerInitializeAsync()
        {
            this.initializedClient = this.options.CosmosClient == null;
            this.cosmosClient = this.GetClientInstance();
            if (this.options.CreateIfNotExists)
            {
                DatabaseResponse databaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(this.options.DatabaseName).ConfigureAwait(false);
                this.options.DiagnosticsHandler?.Invoke(databaseResponse.Diagnostics);

                int defaultTimeToLive = this.options.DefaultTimeToLiveInMs.HasValue
                    && this.options.DefaultTimeToLiveInMs.Value > 0 ? (int)TimeSpan.FromMilliseconds(this.options.DefaultTimeToLiveInMs.Value).TotalSeconds : CosmosCache.DefaultTimeToLive;

                try
                {
                    ContainerResponse existingContainer = await this.cosmosClient.GetContainer(this.options.DatabaseName, this.options.ContainerName).ReadContainerAsync().ConfigureAwait(false);
                    this.options.DiagnosticsHandler?.Invoke(existingContainer.Diagnostics);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    this.options.DiagnosticsHandler?.Invoke(ex.Diagnostics);

                    // Container is optimized as Key-Value store excluding all properties
                    string partitionKeyDefinition = CosmosCache.ContainerPartitionKeyPath;
                    if (!string.IsNullOrWhiteSpace(this.options.ContainerPartitionKeyAttribute))
                    {
                        partitionKeyDefinition = $"/{this.options.ContainerPartitionKeyAttribute}";
                    }

                    ContainerResponse newContainer = await this.cosmosClient.GetDatabase(this.options.DatabaseName).DefineContainer(this.options.ContainerName, partitionKeyDefinition)
                        .WithDefaultTimeToLive(defaultTimeToLive)
                        .WithIndexingPolicy()
                            .WithIndexingMode(IndexingMode.Consistent)
                            .WithIncludedPaths()
                                .Attach()
                            .WithExcludedPaths()
                                .Path("/*")
                                .Attach()
                        .Attach()
                    .CreateAsync(this.options.ContainerThroughput).ConfigureAwait(false);
                    this.options.DiagnosticsHandler?.Invoke(newContainer.Diagnostics);
                }
            }
            else
            {
                try
                {
                    ContainerResponse existingContainer = await this.cosmosClient.GetContainer(this.options.DatabaseName, this.options.ContainerName).ReadContainerAsync().ConfigureAwait(false);
                    this.options.DiagnosticsHandler?.Invoke(existingContainer.Diagnostics);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    this.options.DiagnosticsHandler?.Invoke(ex.Diagnostics);
                    throw new InvalidOperationException($"Cannot find an existing container named {this.options.ContainerName} within database {this.options.DatabaseName}");
                }
            }

            return this.cosmosClient.GetContainer(this.options.DatabaseName, this.options.ContainerName);
        }

        private CosmosClient GetClientInstance()
        {
            if (this.options.CosmosClient != null)
            {
                return this.options.CosmosClient;
            }

            if (this.options.ClientBuilder == null)
            {
                throw new ArgumentNullException(nameof(this.options.ClientBuilder));
            }

            return this.options.ClientBuilder
                    .WithApplicationName(CosmosCache.UseUserAgentSuffix)
                    .Build();
        }
    }
}

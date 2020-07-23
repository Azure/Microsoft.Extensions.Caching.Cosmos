//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Options to configure Microsoft.Extensions.Caching.Cosmos.
    /// </summary>
    public class CosmosCacheOptions : IOptions<CosmosCacheOptions>
    {
        /// <summary>
        /// Gets or sets an instance of <see cref="CosmosClientBuilder"/> to build a Cosmos Client with. Either use this or provide an existing <see cref="CosmosClient"/>.
        /// </summary>
        public CosmosClientBuilder ClientBuilder { get; set; }

        /// <summary>
        /// Gets or sets an existing CosmosClient to use for the storage operations. Either use this or provide a <see cref="ClientBuilder"/> to provision a client.
        /// </summary>
        public CosmosClient CosmosClient { get; set; }

        /// <summary>
        /// Gets or sets the database name to store the cache.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Gets or sets the container name to store the cache.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether initialization it will check for the Container existence and create it if it doesn't exist using <see cref="ContainerThroughput"/> as provisioned throughput and <see cref="DefaultTimeToLiveInMs"/>.
        /// </summary>
        public bool CreateIfNotExists { get; set; }

        /// <summary>
        /// Gets or sets the provisioned throughput for the Container in case <see cref="CreateIfNotExists"/> is true and the Container does not exist.
        /// </summary>
        public int? ContainerThroughput { get; set; }

        /// <summary>
        /// Gets or sets the default Time to Live for the Container in case <see cref="CreateIfNotExists"/> is true and the Container does not exist.
        /// </summary>
        public int? DefaultTimeToLiveInMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to retry failed updates after a Get to an item with sliding expiration.
        /// </summary>
        /// <remarks>
        /// This can be useful for applications with high frequency reads on the same cache item.
        /// </remarks>
        public bool RetrySlidingExpirationUpdates { get; set; } = true;

        /// <summary>
        /// Gets the current options values.
        /// </summary>
        CosmosCacheOptions IOptions<CosmosCacheOptions>.Value
        {
            get { return this; }
        }
    }
}

namespace Microsoft.Extensions.Caching.Cosmos
{
    using System.Security;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Options;

    public class CosmosCacheOptions : IOptions<CosmosCacheOptions>
    {
        /// <summary>
        /// Instance of <see cref="CosmosClientOptions"/> to create a Cosmos Client with. Either use this or provide an existing <see cref="CosmosClient"/>.
        /// </summary>
        public CosmosClientOptions Configuration { get; set; }

        /// <summary>
        /// Instance of <see cref="SecureString"/> that holds the connection string when you are initializing the cache along with <see cref="CosmosCacheOptions.Configuration"/>
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Existing CosmosClient to use for the storage operations. Either use this or provide a <see cref="Configuration"/> to provision a client.
        /// </summary>
        public CosmosClient CosmosClient { get; set; }

        /// <summary>
        /// Database name to store the cache.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// Container name to store the cache.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// It will check for the Container existence and create it if it doesn't exist using <see cref="ContainerThroughput"/> as provisioned throughput and <see cref="DefaultTimeToLiveInMs"/>.
        /// </summary>
        public bool CreateIfNotExists { get; set; }

        /// <summary>
        /// Provisioned throughput for the Container in case <see cref="CreateIfNotExists"/> is true and the Container does not exist.
        /// </summary>
        public int? ContainerThroughput { get; set; }

        /// <summary>
        /// Default Time to Live for the Container in case <see cref="CreateIfNotExists"/> is true and the Container does not exist.
        /// </summary>
        public int? DefaultTimeToLiveInMs { get; set; }

        CosmosCacheOptions IOptions<CosmosCacheOptions>.Value
        {
            get { return this; }
        }
    }
}

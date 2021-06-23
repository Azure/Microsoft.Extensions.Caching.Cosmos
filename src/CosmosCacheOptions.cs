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
        /// Delegate to receive Diagnostics from the internal Cosmos DB operations.
        /// </summary>
        /// <param name="diagnostics">An instance of <see cref="CosmosDiagnostics"/> result from a Cosmos DB service operation.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// void captureDiagnostics(CosmosDiagnostics diagnostics)
        /// {
        ///     if (diagnostics.GetClientElapsedTime() > SomePredefinedThresholdTime)
        ///     {
        ///         Console.WriteLine(diagnostics.ToString());
        ///     }
        /// }
        ///
        /// services.AddCosmosCache((CosmosCacheOptions cacheOptions) =>
        /// {
        ///     cacheOptions.DiagnosticsHandler = captureDiagnostics;
        ///     /* other options */
        /// });
        /// ]]>
        /// </code>
        /// </example>
        public delegate void DiagnosticsDelegate(CosmosDiagnostics diagnostics);

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
        /// <value>Default value is false.</value>
        public bool CreateIfNotExists { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating the name of the property used as Partition Key on the Container.
        /// </summary>
        /// <remarks>
        /// If <see cref="CreateIfNotExists"/> is true, it will be used to define the Partition Key of the created Container.
        /// </remarks>
        /// <value>Default value is "id".</value>
        public string ContainerPartitionKeyAttribute { get; set; }

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
        /// <para>This can be useful for applications with high frequency reads on the same cache item that does not change.</para>
        /// <para>Turning this feature off for cache items that change constantly can lead to dirty reads. </para>
        /// </remarks>
        /// <value>Default value is true.</value>
        public bool RetrySlidingExpirationUpdates { get; set; } = true;

        /// <summary>
        /// Gets or sets a delegate to capture operation diagnostics.
        /// </summary>
        /// <remarks>
        /// <para>This delegate captures the <see cref="CosmosDiagnostics"/> from the operations performed on the Cosmos DB service.</para>
        /// <para>Once set, it will be called for all executed operations and can be used for conditionally capturing diagnostics. </para>
        /// </remarks>
        public DiagnosticsDelegate DiagnosticsHandler { get; set; }

        /// <summary>
        /// Gets the current options values.
        /// </summary>
        CosmosCacheOptions IOptions<CosmosCacheOptions>.Value
        {
            get { return this; }
        }
    }
}

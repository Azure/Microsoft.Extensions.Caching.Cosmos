//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using Microsoft.Extensions.Caching.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for setting up Azure Cosmos DB distributed cache related services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class CosmosCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Azure Cosmos DB distributed caching services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="setupAction">An <see cref="Action{CosmosCacheOptions}"/> to configure the provided
        /// <see cref="CosmosCacheOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddCosmosCache(this IServiceCollection services, Action<CosmosCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            services.Configure(setupAction);
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, CosmosCache>((IServiceProvider provider) =>
            {
                IOptionsMonitor<CosmosCacheOptions> optionsMonitor = provider.GetService<IOptionsMonitor<CosmosCacheOptions>>();
                if (optionsMonitor != null)
                {
                    return new CosmosCache(optionsMonitor);
                }

                IOptions<CosmosCacheOptions> options = provider.GetRequiredService<IOptions<CosmosCacheOptions>>();
                return new CosmosCache(options);
            }));

            return services;
        }
    }
}

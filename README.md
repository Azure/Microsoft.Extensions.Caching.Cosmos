# Microsoft Caching Extension using Azure Cosmos DB

[![Build Status](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_apis/build/status/Microsoft.Extensions.Caching.Cosmos%20-%20Nightly?branchName=master)](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build/latest?definitionId=46&branchName=master)

This repository contains an implementation of `IDistributedCache` using Azure Cosmos DB that can be leveraged in ASP.NET Core as a Session State Provider.

There is also a [sample](./sample/Startup.cs) on how to instantiate the provider as part of ASP.NET Core's pipeline.

## CosmosClient initialization

The implementation provides two distinct options:

### Use an existing instance of a CosmosClient

This option will make the provider re-use an existing `CosmosClient` instance, which won't be disposed when the provider is disposed.

```
services.AddCosmosCache((CosmosCacheOptions cacheOptions) =>
{
    cacheOptions.ContainerName = Configuration["CosmosCacheContainer"];
    cacheOptions.DatabaseName = Configuration["CosmosCacheDatabase"];
    cacheOptions.CosmosClient = existingCosmosClient;
    
    cacheOptions.CreateIfNotExists = true;
});
```

### Use a defined CosmosBuilder

This option will make the provider maintain an internal instance of `CosmosClient` that will get disposed when the provider is disposed. The `CosmosClient` will be created using the provided `CosmosBuilder`.

```
services.AddCosmosCache((CosmosCacheOptions cacheOptions) =>
{
    cacheOptions.ContainerName = Configuration["CosmosCacheContainer"];
    cacheOptions.DatabaseName = Configuration["CosmosCacheDatabase"];
    cacheOptions.ClientBuilder = new CosmosClientBuilder(Configuration["CosmosConnectionString"]);
    
    cacheOptions.CreateIfNotExists = true;
});
```

### State storage

The provider stores the state in a container within a database, both parameters are required within the `CosmosCacheOptions` initialization. An optional parameter, `CreateIfNotExists` will make sure to create the container if it does not exist with an optimized configuration for key-value storage. `ContainerThroughput` can be used to specify a particular Throughput on the container.

### Cosmos account consistency

Consistency in a distributed cache is important, especially when applications are deployed across multiple instances and user requests can be distributed across all of them. 
Azure Cosmos DB has [multiple options](https://docs.microsoft.com/azure/cosmos-db/consistency-levels) when it comes to configuring consistency. When using it as a districuted cache provider, the following consistency levels are recommended:

* Session: Recommended only if the ASP.NET application will be using **Application Request Routing Affinity**. This is enabled by default when hosting your ASP.NET application in [Azure App Service](https://docs.microsoft.com/azure/app-service/configure-common#configure-general-settings).
* Bounded staleness
* Strong

Using other consistency levels is not recommended as read operations could be getting a stale version of the session.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

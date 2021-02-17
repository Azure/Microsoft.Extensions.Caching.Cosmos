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

    public class CosmosCacheTests
    {
        [Fact]
        public void RequiredParameters()
        {
            // Null-check
            Assert.Throws<ArgumentNullException>(() => new CosmosCache(null));

            IOptions<CosmosCacheOptions> options = Options.Create(new CosmosCacheOptions(){});
            // Database
            Assert.Throws<ArgumentNullException>(() => new CosmosCache(options));
            options.Value.DatabaseName = "something";
            // Container
            Assert.Throws<ArgumentNullException>(() => new CosmosCache(options));
            options.Value.ContainerName = "something";
            // ClientBuilder or CosmosClient
            Assert.Throws<ArgumentNullException>(() => new CosmosCache(options));

            // Verify that it creates with all parameters
            new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                ClientBuilder = new CosmosClientBuilder("https://someendpoint.documents.azure.com", "someKey==")
            }));

            new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = new CosmosClient("https://someendpoint.documents.azure.com", "dGVzdA==") // "test" in base64
            }));
        }

        [Fact]
        public async Task ConnectAsyncThrowsIfContainerDoesNotExist()
        {
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0));
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("key"));
        }

        [Fact]
        public async Task GetObtainsSessionAndUpdatesCacheForSlidingExpiration()
        {
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetObtainsSessionAndUpdatesCacheForSlidingExpirationWithAbsoluteExpiration()
        {
            const int ttlSliding = 20;
            const int ttlAbsolute = 5;
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            existingSession.TimeToLive = ttlSliding;
            existingSession.AbsoluteSlidingExpiration = DateTimeOffset.UtcNow.AddSeconds(ttlAbsolute).ToUnixTimeSeconds();
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.TimeToLive <= ttlAbsolute), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetObtainsSessionAndUpdatesCacheForSlidingExpirationWithAbsoluteExpirationWithHigherTime()
        {
            const int ttlSliding = 20;
            const int ttlAbsolute = 50;
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            existingSession.TimeToLive = ttlSliding;
            existingSession.AbsoluteSlidingExpiration = DateTimeOffset.UtcNow.AddSeconds(ttlAbsolute).ToUnixTimeSeconds();
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.TimeToLive == ttlSliding), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetObtainsSessionAndDoesNotUpdatesCacheForAbsoluteExpiration()
        {
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = false;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions()
            {
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetDoesNotRetryUpdateIfRetrySlidingExpirationUpdatesIsFalse()
        {
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CosmosException("test", HttpStatusCode.PreconditionFailed, 0, "", 0));;
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                RetrySlidingExpirationUpdates = false
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetDoesRetryUpdateIfRetrySlidingExpirationUpdatesIsTrue()
        {
            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.SetupSequence(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("test", HttpStatusCode.PreconditionFailed, 0, "", 0))
                .ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                RetrySlidingExpirationUpdates = true
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetReturnsNullIfKeyDoesNotExist()
        {
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0));
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.IsAny<CosmosCacheSession>(), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0));
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            Assert.Null(await cache.GetAsync("key"));
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.IsAny<CosmosCacheSession>(), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveAsyncDeletesItem()
        {
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await cache.RemoveAsync("key");
            mockedContainer.Verify(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task RemoveAsyncNotExistItem()
        {
            var mockedClient = new Mock<CosmosClient>();
            var mockedContainer = new Mock<Container>();
            mockedContainer.Setup(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "not-exist-key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("test remove not exist", HttpStatusCode.NotFound, 0, "", 0))
                .Verifiable();
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object).Verifiable();
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await cache.RemoveAsync("not-exist-key");
            mockedContainer.Verify(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "not-exist-key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedClient.VerifyAll();
            mockedContainer.VerifyAll();
        }

        [Fact]
        public async Task SetAsyncCallsUpsert()
        {
            int ttl = 10;
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await cache.SetAsync(existingSession.SessionKey, existingSession.Content, cacheOptions);
            mockedContainer.Verify(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ValidatesAbsoluteExpiration()
        {
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.AbsoluteExpiration = DateTime.UtcNow.AddHours(-1);
            
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.SetAsync(existingSession.SessionKey, existingSession.Content, cacheOptions));
        }

        [Fact]
        public async Task ValidatesNoExpirationUsesNullTtl()
        {
            long? ttl = null;
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object
            }));

            await cache.SetAsync(existingSession.SessionKey, existingSession.Content, cacheOptions);
            mockedContainer.Verify(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
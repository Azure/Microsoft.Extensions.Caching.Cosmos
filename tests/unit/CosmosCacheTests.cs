//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Caching.Cosmos;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Options;
    using Moq;
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
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            CosmosException exception = new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("key"));
            Assert.Equal(1, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Equal(exception.Diagnostics.ToString(), diagnosticsSink.CapturedDiagnostics[0].ToString());
        }

        [Fact]
        public async Task GetObtainsSessionAndUpdatesCacheForSlidingExpiration()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<DatabaseResponse> mockedDatabaseResponse = new Mock<DatabaseResponse>();
            Mock<CosmosDiagnostics> mockedDatabaseDiagnostics = new Mock<CosmosDiagnostics>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);            
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedDatabaseResponse.Setup(c => c.Diagnostics).Returns(mockedDatabaseDiagnostics.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            mockedClient.Setup(x => x.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedDatabaseResponse.Object);
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(4, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedDatabaseDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[2]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[3]);
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
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = false;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<DatabaseResponse> mockedDatabaseResponse = new Mock<DatabaseResponse>();
            Mock<CosmosDiagnostics> mockedDatabaseDiagnostics = new Mock<CosmosDiagnostics>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedDatabaseResponse.Setup(c => c.Diagnostics).Returns(mockedDatabaseDiagnostics.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            mockedClient.Setup(x => x.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedDatabaseResponse.Object);
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions()
            {
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);

            Assert.Equal(3, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedDatabaseDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[2]);
        }

        [Fact]
        public async Task GetDoesNotRetryUpdateIfRetrySlidingExpirationUpdatesIsFalse()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<DatabaseResponse> mockedDatabaseResponse = new Mock<DatabaseResponse>();
            Mock<CosmosDiagnostics> mockedDatabaseDiagnostics = new Mock<CosmosDiagnostics>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            CosmosException preconditionFailedException = new CosmosException("test", HttpStatusCode.PreconditionFailed, 0, "", 0);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(preconditionFailedException);
            mockedDatabaseResponse.Setup(c => c.Diagnostics).Returns(mockedDatabaseDiagnostics.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            mockedClient.Setup(x => x.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedDatabaseResponse.Object);
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                RetrySlidingExpirationUpdates = false,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(4, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedDatabaseDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[2]);
            Assert.Equal(preconditionFailedException.Diagnostics.ToString(), diagnosticsSink.CapturedDiagnostics[3].ToString());
        }

        [Fact]
        public async Task GetDoesRetryUpdateIfRetrySlidingExpirationUpdatesIsTrue()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            string etag = "etag";
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            existingSession.IsSlidingExpiration = true;
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<DatabaseResponse> mockedDatabaseResponse = new Mock<DatabaseResponse>();
            Mock<CosmosDiagnostics> mockedDatabaseDiagnostics = new Mock<CosmosDiagnostics>();
            mockedItemResponse.Setup(c => c.Resource).Returns(existingSession);
            mockedItemResponse.Setup(c => c.ETag).Returns(etag);
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            CosmosException preconditionException = new CosmosException("test", HttpStatusCode.PreconditionFailed, 0, "", 0);
            mockedContainer.SetupSequence(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(preconditionException)
                .ReturnsAsync(mockedItemResponse.Object);
            mockedDatabaseResponse.Setup(c => c.Diagnostics).Returns(mockedDatabaseDiagnostics.Object);    
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            mockedClient.Setup(x => x.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedDatabaseResponse.Object);
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CreateIfNotExists = true,
                CosmosClient = mockedClient.Object,
                RetrySlidingExpirationUpdates = true,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            Assert.Same(existingSession.Content, await cache.GetAsync("key"));
            // Checks for Db existence due to CreateIfNotExists
            mockedClient.Verify(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item == existingSession), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.Equal(6, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedDatabaseDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[2]);
            Assert.Equal(preconditionException.Diagnostics.ToString(), diagnosticsSink.CapturedDiagnostics[3].ToString());
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[4]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[5]);
        }

        [Fact]
        public async Task GetReturnsNullIfKeyDoesNotExist()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            CosmosException notFoundException = new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0);
            mockedContainer.Setup(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(notFoundException);
            mockedContainer.Setup(c => c.ReplaceItemAsync<CosmosCacheSession>(It.IsAny<CosmosCacheSession>(), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ThrowsAsync(new CosmosException("test", HttpStatusCode.NotFound, 0, "", 0));
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            Assert.Null(await cache.GetAsync("key"));
            mockedContainer.Verify(c => c.ReadItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedContainer.Verify(c => c.ReplaceItemAsync<CosmosCacheSession>(It.IsAny<CosmosCacheSession>(), It.Is<string>(id => id == "key"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);

            Assert.Equal(2, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Equal(notFoundException.Diagnostics.ToString(), diagnosticsSink.CapturedDiagnostics[1].ToString());
        }

        [Fact]
        public async Task RemoveAsyncDeletesItem()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            await cache.RemoveAsync("key");
            mockedContainer.Verify(c => c.DeleteItemAsync<CosmosCacheSession>(It.Is<string>(id => id == "key"), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(2, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
        }
        
        [Fact]
        public async Task RemoveAsyncNotExistItem()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            var mockedClient = new Mock<CosmosClient>();
            var mockedContainer = new Mock<Container>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            CosmosException notExistException = new CosmosException("test remove not exist", HttpStatusCode.NotFound, 0, "", 0);
            mockedContainer.Setup(c => c.DeleteItemAsync<CosmosCacheSession>("not-exist-key", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(notExistException)
                .Verifiable();
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object).Verifiable();
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            await cache.RemoveAsync("not-exist-key");
            mockedContainer.Verify(c => c.DeleteItemAsync<CosmosCacheSession>("not-exist-key", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            mockedClient.VerifyAll();
            mockedContainer.VerifyAll();

            Assert.Equal(2, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Equal(notExistException.Diagnostics.ToString(), diagnosticsSink.CapturedDiagnostics[1].ToString());
        }

        [Fact]
        public async Task SetAsyncCallsUpsert()
        {
            DiagnosticsSink diagnosticsSink = new DiagnosticsSink();

            int ttl = 10;
            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions();
            cacheOptions.SlidingExpiration = TimeSpan.FromSeconds(ttl);
            
            CosmosCacheSession existingSession = new CosmosCacheSession();
            existingSession.SessionKey = "key";
            existingSession.Content = new byte[0];
            Mock<ItemResponse<CosmosCacheSession>> mockedItemResponse = new Mock<ItemResponse<CosmosCacheSession>>();
            Mock<CosmosDiagnostics> mockedItemDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<CosmosClient> mockedClient = new Mock<CosmosClient>();
            Mock<Container> mockedContainer = new Mock<Container>();
            Mock<CosmosDiagnostics> mockedContainerDiagnostics = new Mock<CosmosDiagnostics>();
            Mock<Database> mockedDatabase = new Mock<Database>();
            Mock<ContainerResponse> mockedResponse = new Mock<ContainerResponse>();
            mockedResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            mockedResponse.Setup(c => c.Diagnostics).Returns(mockedContainerDiagnostics.Object);
            mockedItemResponse.Setup(c => c.Diagnostics).Returns(mockedItemDiagnostics.Object);
            mockedContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);
            mockedContainer.Setup(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockedItemResponse.Object);
            mockedClient.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>())).Returns(mockedContainer.Object);
            mockedClient.Setup(c => c.GetDatabase(It.IsAny<string>())).Returns(mockedDatabase.Object);
            mockedClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));
            CosmosCache cache = new CosmosCache(Options.Create(new CosmosCacheOptions(){
                DatabaseName = "something",
                ContainerName = "something",
                CosmosClient = mockedClient.Object,
                DiagnosticsHandler = diagnosticsSink.CaptureDiagnostics
            }));

            await cache.SetAsync(existingSession.SessionKey, existingSession.Content, cacheOptions);
            mockedContainer.Verify(c => c.UpsertItemAsync<CosmosCacheSession>(It.Is<CosmosCacheSession>(item => item.SessionKey == existingSession.SessionKey && item.TimeToLive == ttl), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal(2, diagnosticsSink.CapturedDiagnostics.Count);
            Assert.Same(mockedContainerDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[0]);
            Assert.Same(mockedItemDiagnostics.Object, diagnosticsSink.CapturedDiagnostics[1]);
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
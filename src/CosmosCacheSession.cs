//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using Microsoft.Extensions.Caching.Distributed;
    using Newtonsoft.Json;

    [JsonConverter(typeof(CosmosCacheSessionConverter))]
    internal class CosmosCacheSession
    {
        public string SessionKey { get; set; }

        public byte[] Content { get; set; }

        public long? TimeToLive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="TimeToLive"/> is sliding or absolute.<br/>
        /// True if <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> was set and used for the TTL,
        /// false if <see cref="DistributedCacheEntryOptions.AbsoluteExpiration"/> or <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/> was set and used for the TTL;
        /// otherwise null.
        /// </summary>
        public bool? IsSlidingExpiration { get; set; }

        public string PartitionKeyAttribute { get; set; }
    }
}

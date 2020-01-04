//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Extensions.Caching.Cosmos
{
    using Microsoft.Extensions.Caching.Distributed;
    using Newtonsoft.Json;

    internal class CosmosCacheSession
    {
        [JsonProperty("id")]
        public string SessionKey { get; set; }

        [JsonProperty("content")]
        public byte[] Content { get; set; }

        [JsonProperty("ttl")]
        public long? TimeToLive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="TimeToLive"/> is sliding or absolute.<br/>
        /// True if <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> was set and used for the TTL,
        /// false if <see cref="DistributedCacheEntryOptions.AbsoluteExpiration"/> or <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/> was set and used for the TTL;
        /// otherwise null.
        /// </summary>
        [JsonProperty("isSlidingExpiration")]
        public bool? IsSlidingExpiration { get; set; }
    }
}

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
        /// Gets or sets a value indicating whether the <see cref="TimeToLive"/> was generated with sliding expiration active.<br/>
        /// True if <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> was set.
        /// </summary>
        public bool? IsSlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the absolute expiration of an item to be used when the item had <see cref="DistributedCacheEntryOptions.SlidingExpiration"/> and <see cref="DistributedCacheEntryOptions.AbsoluteExpiration"/> or <see cref="DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow"/>.
        /// </summary>
        public long? AbsoluteSlidingExpiration { get; set; }

        public string PartitionKeyAttribute { get; set; }
    }
}

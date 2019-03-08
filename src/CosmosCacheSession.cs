namespace Microsoft.Extensions.Caching.Cosmos
{
    using Newtonsoft.Json;

    internal class CosmosCacheSession
    {
        [JsonProperty("id")]
        public string SessionKey { get; set; }

        [JsonProperty("content")]
        public byte[] Content { get; set; }

        [JsonProperty("ttl")]
        public long? TimeToLive { get; set; }
    }
}

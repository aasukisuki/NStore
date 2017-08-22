namespace NStore.Persistence.DocumentDb
{
    using Newtonsoft.Json;

    internal class Counter
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("value")]
        public long Value { get; set; }
    }
}

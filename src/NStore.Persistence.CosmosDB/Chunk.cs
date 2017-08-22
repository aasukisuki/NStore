namespace NStore.Persistence.DocumentDb
{
    using Newtonsoft.Json;
    using System;

    internal class Chunk: IChunk
    {
        internal Chunk()
        {

        }

        [JsonProperty("partitionId")]
        public string PartitionId { get; set; }

        [JsonProperty("position")]
        public long Position { get; set; }

        [JsonProperty("index")]
        public long Index { get; set; }
         
        [JsonProperty("payload")]
        public object Payload { get; set; }

        [JsonProperty("payloadType")]
        public Type PayloadType { get; set; }

        [JsonProperty("operationId")]
        public string OperationId { get; set; }

        [JsonProperty("id")]
        public string Id {
            get{
                return $"{PartitionId}_{Index}";
            }
        }
    }
}

namespace NStore.Persistence.DocumentDb
{
    using System;

    public class DocumentDbOptions
    {
        // Defaults go against the emulator
        public string AccountName { get; set; } = "https://localhost:8081";
        public string AccountKey { get; set; } = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

        public string DatabaseId { get; set; } = "Events";

        public string ChunksCollectionName { get; set; } = "Chunks";
        public string SequenceCollectionName { get; set; } = "Sequences";
        public string OperationsCollectionName { get; set; } = "Operations";

        public string SequenceId { get; set; } = "Streams";

        public bool DropOnInit { get; set; } = false;

        public bool UseLocalSequence { get; set; } = false;

        public ISerializer Serializer { get; set; }

        public bool IsValid()
        {
            return !String.IsNullOrWhiteSpace(AccountKey) && !String.IsNullOrWhiteSpace(AccountName);
        }
    }
}

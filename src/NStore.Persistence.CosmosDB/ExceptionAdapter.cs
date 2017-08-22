namespace NStore.Persistence.DocumentDb
{
    using Microsoft.Azure.Documents;

    internal class ExceptionAdapter
    {
        public void Handle(DocumentClientException ex, IChunk chunk)
        {
            switch (ex.Error.Code.ToLowerInvariant())
            {
                case "conflict":
                    throw new DuplicateStreamIndexException(chunk.PartitionId, chunk.Index);
            }
        }
    }
}

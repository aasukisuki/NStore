namespace NStore.Persistence.Tests.DocumentDb
{
    using NStore.Persistence.DocumentDb;
    using System;
    using System.Threading;

    public partial class BasePersistenceTest
    {
        private DocumentDbPersistence _cosmosDbPersistence;
        private DocumentDbOptions _cosmosDbOptions;

        private static int _staticId = 1;
        private int _id;

        private const string TestSuitePrefix = "DocumentDb";

        static BasePersistenceTest()
        {

        }

        private IPersistence Create()
        {
            _id = Interlocked.Increment(ref _staticId);
            var nameId = Guid.NewGuid().ToString();

            _cosmosDbOptions = new DocumentDbOptions
            {
                DatabaseId = "Events_" + nameId,
                DropOnInit = true
            };

            _cosmosDbPersistence = new DocumentDbPersistence(_cosmosDbOptions);

            _cosmosDbPersistence.InitAsync().Wait();

            return _cosmosDbPersistence;
        }

        private void Clear()
        {
            // nothing to do
            _cosmosDbPersistence.Drop().Wait();
        }
    }
}

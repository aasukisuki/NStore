namespace NStore.Persistence.DocumentDb
{
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class DocumentDbPersistence : IPersistence
    {
        private DocumentDbOptions _options;
        private DocumentClient _client;

        private Uri _databaseUri;
       
        private Uri _chunksUri;
        private Uri _sequenceUri;
        private Uri _operationsUri;

        private ISerializer _serializer;

        private ExceptionAdapter exceptionAdapter = new ExceptionAdapter();

        public DocumentDbPersistence(DocumentDbOptions options)
        {
            if (options == null || !options.IsValid())
            { 
                throw new Exception("Invalid options");
            }

            _options = options;
            _serializer = _options.Serializer ?? new JsonNetSerializer();
        }

        public bool SupportsFillers => true;

        public async Task InitAsync()
        {
            _databaseUri = UriFactory.CreateDatabaseUri(_options.DatabaseId);

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
            serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            _client = new DocumentClient(new Uri(_options.AccountName), _options.AccountKey, serializerSettings);

            var database = _client.CreateDatabaseQuery().Where(db => db.Id == _options.DatabaseId).AsEnumerable().FirstOrDefault();

            if (_options.DropOnInit && database != null)
            {
                await _client.DeleteDatabaseAsync(_databaseUri);
                database = null;
            }
            
            if(database == null)
            {
                database = await _client.CreateDatabaseAsync(new Database() { Id = _options.DatabaseId });

                await _client.CreateDocumentCollectionIfNotExistsAsync(
                    _databaseUri,
                    new DocumentCollection { Id = _options.ChunksCollectionName },
                    new RequestOptions { OfferThroughput = 400 });

                _chunksUri = UriFactory.CreateDocumentCollectionUri(_options.DatabaseId, _options.ChunksCollectionName);

                await _client.CreateDocumentCollectionIfNotExistsAsync(
                    _databaseUri,
                    new DocumentCollection { Id = _options.SequenceCollectionName },
                    new RequestOptions { OfferThroughput = 400 });

                _sequenceUri = UriFactory.CreateDocumentCollectionUri(_options.DatabaseId, _options.SequenceCollectionName);

                await _client.CreateDocumentCollectionIfNotExistsAsync(
                    _databaseUri,
                    new DocumentCollection { Id = _options.OperationsCollectionName },
                    new RequestOptions { OfferThroughput = 400 });

                _operationsUri = UriFactory.CreateDocumentCollectionUri(_options.DatabaseId, _options.OperationsCollectionName);
            }
        }

        public async Task<IChunk> AppendAsync(string partitionId, long index, object payload, string operationId, CancellationToken cancellationToken)
        {
            var id = await GetNextId(_options.SequenceId, cancellationToken);

            if (index < 0)
            {
                index = await GetNextId(partitionId, cancellationToken);
            }
            
            var chunk = new Chunk()
            {
                Position = id,
                PartitionId = partitionId,
                Index = index,
                Payload = _serializer.Serialize(payload),
                PayloadType = payload.GetType() ?? null,
                OperationId = operationId ?? Guid.NewGuid().ToString()
            };

            await AppendChunk(chunk);

            return chunk;
        }

        public async Task DeleteAsync(string partitionId, long fromLowerIndexInclusive, long toUpperIndexInclusive, CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .Where(c => c.PartitionId.Equals(partitionId));

            if(fromLowerIndexInclusive > 0)
            {
                sequence = sequence.Where(c => c.Index >= fromLowerIndexInclusive);
            }

            if(toUpperIndexInclusive < long.MaxValue)
            {
                sequence = sequence.Where(c => c.Index <= toUpperIndexInclusive);
            }

            foreach(var chunk in sequence)
            {
                var docUri = UriFactory.CreateDocumentUri(_options.DatabaseId, _options.ChunksCollectionName, chunk.Id);
                var result = await _client.DeleteDocumentAsync(docUri).ConfigureAwait(false);

                //TODO - If error... 
                // throw new StreamDeleteException(partitionId);
            }
        }

        public async Task ReadAllAsync(long fromSequenceIdInclusive, ISubscription subscription, int limit, CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .Where(c => c.Position >= fromSequenceIdInclusive)
                .OrderBy(c => c.Position)
                .Take(limit);

           await PushToSubscriber(fromSequenceIdInclusive, subscription, sequence, cancellationToken).ConfigureAwait(false);
        }

        public Task<IChunk> ReadLast(string partitionId, long toUpperIndexInclusive, CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .Where(c => c.PartitionId.Equals(partitionId) && c.Index <= toUpperIndexInclusive)
                .OrderByDescending(c => c.Index)
                .Take(1)
                .ToList()
                .SingleOrDefault();

            return Task.FromResult<IChunk>(sequence);
        }

        public Task<long> ReadLastPositionAsync(CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .OrderByDescending(c => c.Position)
                .Take(1)
                .ToList()
                .SingleOrDefault();

            var position = !Equals(sequence, default(Chunk)) ? sequence.Position : 0;

            return Task.FromResult(position);
        }

        public async Task ReadPartitionBackward(string partitionId, long fromUpperIndexInclusive, ISubscription subscription, long toLowerIndexInclusive, int limit, CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .Where(c => c.PartitionId.Equals(partitionId) && c.Index <= fromUpperIndexInclusive && c.Index >= toLowerIndexInclusive)
                .OrderByDescending(c => c.Index);

            await PushToSubscriber(fromUpperIndexInclusive, subscription, sequence, cancellationToken).ConfigureAwait(false);
        }

        public async Task ReadPartitionForward(string partitionId, long fromLowerIndexInclusive, ISubscription subscription, long toUpperIndexInclusive, int limit, CancellationToken cancellationToken)
        {
            var sequence = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                .Where(c => c.PartitionId.Equals(partitionId) && c.Index >= fromLowerIndexInclusive && c.Index <= toUpperIndexInclusive)
                .OrderBy(c => c.Index);

            await PushToSubscriber(fromLowerIndexInclusive, subscription, sequence, cancellationToken).ConfigureAwait(false);
        }

        public async Task Drop()
        {
            //await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_options.DatabaseId, _options.ChunksCollectionId));
            //await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_options.DatabaseId, _options.SequenceCollectionId));

            await _client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(_options.DatabaseId));
        }

        private async Task<long> GetNextId(string partitionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            Counter currentSequence;

            if (!partitionId.Equals(_options.SequenceId))
            {
                var maxIndex = _client.CreateDocumentQuery<Chunk>(_chunksUri)
                    .Where(x => x.PartitionId.Equals(partitionId))
                    .Max(c => c.Index);

                currentSequence = new Counter() {
                    Id = partitionId,
                    Value = maxIndex
                };

            }
            else { 
                currentSequence = _client.CreateDocumentQuery<Counter>(_sequenceUri)
                .Where(x => x.Id.Equals(partitionId))
                .ToList()
                .SingleOrDefault();
            }

            if (Equals(currentSequence, default(Counter))){
                currentSequence = new Counter();
            }

            currentSequence.Value++;

            var docUri = UriFactory.CreateDocumentUri(_options.DatabaseId, _options.SequenceCollectionName, partitionId);
            if (currentSequence.Value > 1)
            {               
                await _client.ReplaceDocumentAsync(docUri, currentSequence);
            }
            else
            {
                currentSequence.Id = partitionId;
                await _client.CreateDocumentAsync(_sequenceUri, currentSequence);
            }

            return currentSequence.Value;
        }

        private async Task PushToSubscriber(long start, ISubscription subscription, IQueryable<Chunk> chunks, CancellationToken cancellationToken)
        {
            long position = 0;
            await subscription.OnStart(start).ConfigureAwait(false);

            foreach(var chunk in chunks)
            {
                position = chunk.Position;
                chunk.Payload = _serializer.Deserialize((string)chunk.Payload, chunk.PayloadType);

                if (!await subscription.OnNext(chunk).ConfigureAwait(false))
                {
                    await subscription.Stopped(position).ConfigureAwait(false);
                    return;
                }
            }

            await subscription.Completed(position).ConfigureAwait(false);
        }

        private async Task AppendChunk(IChunk chunk)
        {
            try { 
                // Record the operation to make sure it's unique
                await _client.CreateDocumentAsync(_operationsUri, new { id = $"{chunk.PartitionId}_{chunk.OperationId}" });
            }
            catch(DocumentClientException e)
            {
                // Operation already comitted, ignore it
                if (e.Error.Code.ToLower().Equals("conflict"))
                {
                    return;
                }
                else
                {
                    throw e;
                }                
            }

            try
            {
                Document created = await _client.CreateDocumentAsync(_chunksUri, chunk);
            }
            catch (DocumentClientException e)
            {
                this.exceptionAdapter.Handle(e, chunk);
            }
        }
    }
}

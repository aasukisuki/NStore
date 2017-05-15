using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NStore.Aggregates;
using NStore.InMemory;
using NStore.Sample.Domain.Room;
using NStore.Sample.Support;

namespace NStore.Sample.Projections
{
    public class RoomsOnSaleProjection : AsyncProjector
    {
        private readonly IReporter _reporter;
        private readonly IDelayer _delayer;

        public class RoomsOnSale
        {
            public string Id { get; set; }
            public string RoomNumber { get; set; }
        }
        private readonly IDictionary<string, RoomsOnSale> _all = new Dictionary<string, RoomsOnSale>();

        public RoomsOnSaleProjection(IReporter reporter, IDelayer delayer)
        {
            _reporter = reporter;
            _delayer = delayer;
        }

        public IEnumerable<RoomsOnSale> List => _all.Values;

        public async Task On(RoomMadeAvailable e)
        {
            _all.Add(e.Id, new RoomsOnSale { Id = e.Id });
            var elapsed = await _delayer.Wait().ConfigureAwait(false);
            this._reporter.Report($"Room available {e.Id} took {elapsed}ms");
        }
    }
}
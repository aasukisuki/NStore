using System.Threading.Tasks;

namespace NStore.InMemory
{
    public class NoNetworkLatencySimulator : INetworkSimulator
    {
        public Task<long> WaitFast()
        {
            return Task.FromResult(0L);
        }

        public Task<long> Wait()
        {
            return Task.FromResult(0L);
        }
    }   
}
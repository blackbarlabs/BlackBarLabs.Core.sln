using System.Threading.Tasks;

namespace BlackBarLabs.Collections.Async
{
    internal class IteratorAsyncTest<TDelegate> : IIteratorAsync<TDelegate>
    {
        private YieldCallbackAsync<TDelegate> yieldAsync;

        internal IteratorAsyncTest(YieldCallbackAsync<TDelegate> yieldAsync)
        {
            this.yieldAsync = yieldAsync;
        }
        
        #region IIteratorAsync

        public async Task IterateAsync(TDelegate callback)
        {
            await yieldAsync(callback);
        }

        #endregion
    }
}

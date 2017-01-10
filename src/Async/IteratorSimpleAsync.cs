using System.Threading.Tasks;

namespace BlackBarLabs.Collections.Async
{
    internal class IteratorSimpleAsync<TDelegate> : IIteratorAsync<TDelegate>
    {
        private YieldCallbackAsync<TDelegate> yieldAsync;

        internal IteratorSimpleAsync(YieldCallbackAsync<TDelegate> yieldAsync)
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

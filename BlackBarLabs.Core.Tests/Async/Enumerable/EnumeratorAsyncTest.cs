using System;
using System.Threading;
using System.Threading.Tasks;

using BlackBarLabs.Collections.Async;

namespace BlackBarLabs.Core.Tests
{
    public class EnumeratorAsyncTest : IEnumeratorAsync<TestDelegateAsync>
    {
        private Barrier callbackBarrier = new Barrier(2);

        private TestDelegateAsync totalCallback;
        private Task yieldAsyncTask;
        private Task callbackTask;

        public EnumeratorAsyncTest(YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            yieldAsyncTask = Task.Run(async () =>
            {
                var xm = Generators.GetSandwichDelegate();
                await xm.Invoke(
                    () =>
                    {
                        callbackBarrier.SignalAndWait();
                        return this.totalCallback;
                    },
                    yieldAsync,
                    (updatedCallbackTask) =>
                    {
                        callbackTask = updatedCallbackTask;
                        callbackBarrier.SignalAndWait();
                        return Task.FromResult(true);
                    });
                callbackBarrier.RemoveParticipant();
            });
        }

        #region IEnumeratorAsync

        public async Task<bool> MoveNextAsync(TestDelegateAsync callback)
        {
            totalCallback = callback;
            callbackBarrier.SignalAndWait();
            if (yieldAsyncTask.IsCompleted)
                return false;
            callbackBarrier.SignalAndWait();
            await callbackTask;
            return true;
        }

        public Task ResetAsync()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace BlackBarLabs.Collections.Async
{
    public class EnumeratorNonAsync<TResult, TDelegateItems, TDelegateConvert> : IEnumerator<TResult>
    {
        private Barrier callbackBarrier = new Barrier(2);

        private Task yieldAsyncTask;

        private bool complete = false;
        private Exception exception = default(Exception);


        public EnumeratorNonAsync(YieldCallbackAsync<TDelegateItems> yieldAsync,
            TDelegateConvert resultCallback)
        {
            yieldAsyncTask = Task.Run(async () =>
            {
                try
                {
                    await yieldAsync.ResultSwapInvoke<TResult, TDelegateItems, TDelegateConvert>(
                    () =>
                    {
                        callbackBarrier.SignalAndWait();
                        return resultCallback;
                    },
                    (updatedCallbackTask) =>
                    {
                        this.Current = updatedCallbackTask;
                        callbackBarrier.SignalAndWait();
                        return Task.FromResult(true);
                    });
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw ex;
                }
                finally
                {
                    complete = true;
                    callbackBarrier.RemoveParticipant();
                }
            });
        }

        #region IEnumerator<TResult>

        public TResult Current { get; private set; }

        object IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        public void Dispose()
        {
            // TODO: Dump the yieldAsyncTask;
        }

        public bool MoveNext()
        {
            callbackBarrier.SignalAndWait(); // Signal to start updating current
            if (default(Exception) != exception)
                throw exception;
            if (this.complete)
                return false;
            callbackBarrier.SignalAndWait(); // Wait until current is updated
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class EnumeratorStructNonAsync<TStruct> : IEnumerator<TStruct>
    {
        // private YieldStructDelegateAsync<TStruct> yieldAsync;
        private Task yieldAsyncTask;
        private Barrier callbackBarrier = new Barrier(2);

        private bool complete = false;
        private Exception exception = default(Exception);

        public EnumeratorStructNonAsync(YieldStructDelegateAsync<TStruct> yieldAsync)
        {
            yieldAsyncTask = Task.Run(async () =>
            {
                try
                {
                    await yieldAsync(
                        (item) =>
                        {
                            callbackBarrier.SignalAndWait();
                            this.Current = item;
                            callbackBarrier.SignalAndWait();
                            return Task.FromResult(true);
                        });
                }
                catch (Exception ex)
                {
                    exception = ex;
                    throw ex;
                }
                finally
                {
                    complete = true;
                    callbackBarrier.RemoveParticipant();
                }
            });
        }

        TStruct _current;
        public TStruct Current
        {
            get { return _current; }
            private set { _current = value; }
        }

        object IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        public void Dispose()
        {
            // TODO: Dump the yieldAsyncTask;
        }

        public bool MoveNext()
        {
            callbackBarrier.SignalAndWait(); // Signal to start updating current
            if (default(Exception) != exception)
                throw exception;
            if (this.complete)
                return false;
            callbackBarrier.SignalAndWait(); // Wait until current is updated
            return true;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using BlackBarLabs.Collections.Async;

namespace BlackBarLabs.Core.Tests
{
    public class EnumerableTest : IEnumerable<int>
    {
        private TestDelegateResult<int> convertDelegate;
        private YieldCallbackAsync<TestDelegateAsync> yieldAsync;

        public EnumerableTest(YieldCallbackAsync<TestDelegateAsync> yieldAsync, TestDelegateResult<int> convertDelegate)
        {
            this.yieldAsync = yieldAsync;
            this.convertDelegate = convertDelegate;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return new IteratorAsyncTest(this.yieldAsync, this.convertDelegate);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public static class EmptyClass2
    {
        public static IEnumerable<int> ToEnumerableX(this IEnumerableAsync<TestDelegateAsync> items,
            TestDelegateResult<int> convert)
        {
            var iterator = ((EnumerableAsyncTest)items).GetEnumerable(convert);
            return iterator;
        }

        public static ResultSwapDelegate<int, TestDelegateAsync, TestDelegateResult<int>> GetResultSwapDelegate()
        {
            ResultSwapDelegate<int, TestDelegateAsync, TestDelegateResult<int>> resultSwapDelegate =
                (preCallback, yieldA, postCallback) =>
                {
                    TestDelegateAsync invoked = (a, b, c) =>
                    {
                        var pre = preCallback.Invoke();
                        var preResult = pre.Invoke(a, b, c);
                        var delegateTask = postCallback.Invoke(preResult);
                        return delegateTask;
                    };
                    var yieldTask = yieldA.Invoke(invoked);
                    return yieldTask;
                };
            return resultSwapDelegate;
        }
    }

    public class IteratorAsyncTest : IEnumerator<int>
    {
        private Barrier callbackBarrier = new Barrier(2);
        
        private Task yieldAsyncTask;

        public int Current { get; private set; }
        private bool complete = false;

        object IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        public IteratorAsyncTest(YieldCallbackAsync<TestDelegateAsync> yieldAsync,
            TestDelegateResult<int> resultCallback)
        {
            yieldAsyncTask = Task.Run(async () =>
            {
                var xm = EmptyClass2.GetResultSwapDelegate();
                await xm.Invoke(
                    () =>
                    {
                        callbackBarrier.SignalAndWait();
                        return resultCallback;
                    },
                    yieldAsync,
                    (updatedCallbackTask) =>
                    {
                        this.Current = updatedCallbackTask;
                        callbackBarrier.SignalAndWait();
                        return Task.FromResult(true);
                    });
                complete = true;
                callbackBarrier.RemoveParticipant();
            });
        }

        #region IEnumeratorAsync

        public void Dispose()
        {
            // TODO: Dump the yieldAsyncTask;
        }

        public bool MoveNext()
        {
            callbackBarrier.SignalAndWait(); // Signal to start updating current
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

    public delegate Task<T> TestDelegateResultAsync<T>(int a, string b, List<int> c);
    public delegate T TestDelegateResult<T>(int a, string b, List<int> c);

    [TestClass]
    public class IIterateAsyncTests
    {
        [TestMethod]
        public void IterateAsyncTests()
        {
            var items = EnumerableAsyncTest.YieldAsync(
                async (yield) =>
                {
                    Thread.Sleep(1000);
                    var tasks = new List<Task>();
                    for (int i = 0; i < 100; i++)
                    {
                        var yieldTask = yield(i, "foo", new List<int>());
                        await yieldTask;
                    }
                });
            var results = items.ToEnumerableX(
                (a, b, c) =>
                {
                    Thread.Sleep(20);
                    return a;
                });
            Assert.AreEqual(100, results.Count());
        }

        [TestMethod]
        public void IterateAsyncGenericTests()
        {
            var items = EnumerableAsync.YieldAsync<TestDelegateAsync>(
                async (yield) =>
                {
                    Thread.Sleep(1000);
                    var tasks = new List<Task>();
                    for (int i = 0; i < 100; i++)
                    {
                        var yieldTask = yield(i, "foo", new List<int>());
                        await yieldTask;
                    }
                });
            var results = items.ToEnumerable<TestDelegateAsync, TestDelegateResult<int>, int>(
                (a, b, c) =>
                {
                    Thread.Sleep(20);
                    return a;
                });
            Assert.AreEqual(100, results.Count());
        }
    }
}

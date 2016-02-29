using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using BlackBarLabs.Collections.Async;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

namespace BlackBarLabs.Core.Tests
{
    

    public class EnumerableAsyncTest : IEnumerableAsync<TestDelegateAsync>
    {
        public static IEnumerableAsync<TestDelegateAsync> YieldAsync(EnumerableAsync.YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            return new EnumerableAsyncTest(yieldAsync);
        }

        private EnumerableAsync.YieldCallbackAsync<TestDelegateAsync> yieldAsync;

        public EnumerableAsyncTest(EnumerableAsync.YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            this.yieldAsync = yieldAsync;
        }

        public IEnumeratorAsync<TestDelegateAsync> GetEnumerator()
        {
            return new EnumeratorAsyncTest(this.yieldAsync);
        }
    }

    public static class EmptyClass
    {
        public static Func<Barrier, Func<TestDelegateAsync>, EnumerableAsync.YieldCallbackAsync<TestDelegateAsync>, Action<Task>, Task> GetXM()
        {
            Func<Barrier, Func<TestDelegateAsync>, EnumerableAsync.YieldCallbackAsync<TestDelegateAsync>, Action<Task>, Task> xm =
                (barrier, totalCallbackCallback, yieldA, setCallbackTask) =>
                {
                    TestDelegateAsync invoked = (a, b, c) =>
                    {
                        barrier.SignalAndWait();
                        var tc = totalCallbackCallback.Invoke();
                        var callbackTask = tc.Invoke(a, b, c);
                        setCallbackTask.Invoke(callbackTask);
                        barrier.SignalAndWait();
                        return Task.FromResult(true);
                    };
                    var task = yieldA.Invoke(invoked);
                    return task;
                };
            return xm;
        }
    }

    public class EnumeratorAsyncTest : IEnumeratorAsync<TestDelegateAsync>
    {
        private Barrier callbackBarrier = new Barrier(2);

        private TestDelegateAsync totalCallback;
        private Task yieldAsyncTask;
        private Task callbackTask;

        public EnumeratorAsyncTest(EnumerableAsync.YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            yieldAsyncTask = Task.Run(async () =>
            {
                var xm = EmptyClass.GetXM();
                await xm.Invoke(
                    callbackBarrier,
                    () => this.totalCallback,
                    yieldAsync,
                    (updatedCallbackTask) => { callbackTask = updatedCallbackTask; });
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

        #endregion
    }

    public delegate Task TestDelegateAsync(int a, string b, List<int> c);
    public delegate void TestDelegate(int a, string b, List<int> c);

    [TestClass]
    public class IEnumerableAsyncTests
    {
        
        [TestMethod]
        public async Task EnumerableAsyncTests()
        {
            var items = EnumerableAsyncTest.YieldAsync(
                async (yield) =>
                {
                    var rand = new Random();
                    for (int i = 0; i < 100; i++)
                    {
                        await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                        yield(i, "foo", new List<int>());
                    }

                    yield(110, "bar", new List<int>());
                    yield(111, "food", new List<int>());
                    yield(112, "barf", new List<int>());
                });
            int count = 0;
            await items.SelectAsync(
                async (a, b, c) =>
                {
                    var rand = new Random();
                    await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                    count++;
                });
            Assert.AreEqual(103, count);
        }

        [TestMethod]
        public async Task EnumerableAsyncGerenicTests()
        {
            var items = EnumerableAsync.YieldAsync<TestDelegateAsync> (
                async (yield) =>
                {
                    var rand = new Random();
                    for (int i = 0; i < 100; i++)
                    {
                        await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                        yield(i, "foo", new List<int>());
                    }

                    yield(110, "bar", new List<int>());
                    yield(111, "food", new List<int>());
                    yield(112, "barf", new List<int>());
                });

            int count = 0;
            await items.SelectAsync(
                async (a, b, c) =>
                {
                    await Task.FromResult(true);
                    count++;
                });
            Assert.AreEqual(103, count);
        }
        
        [TestMethod]
        public async Task EnumerableAsyncNotAsyncTests()
        {
            var items = EnumerableAsync.YieldAsync<TestDelegate>(
                async (yield) =>
                {
                    yield(112, "barf", new List<int>());
                });

            try
            {
                await items.SelectAsync(
                    async (a, b, c) => await Task.FromResult(true));
                Assert.Fail("Cannot have non-async IEnumerableAsync delegates");
            } catch(ArgumentException ex)
            {
                Assert.AreEqual(typeof(TestDelegate).FullName, ex.ParamName);
            }
        }
    }
}

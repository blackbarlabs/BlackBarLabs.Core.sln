using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using BlackBarLabs.Collections.Async;

namespace BlackBarLabs.Core.Tests
{
    public delegate Task TestDelegateAsync(int a, string b, List<int> c);
    public delegate void TestDelegate(int a, string b, List<int> c);

    [TestClass]
    public class IEnumerableAsyncTests
    {
        
        [TestMethod]
        public async Task EnumerableAsyncTests()
        {
            var rand = new Random();
            var items = EnumerableAsyncTest.YieldAsync(
                async (yield) =>
                {
                    var tasks = new List<Task>();
                    for (int i = 0; i < 100; i++)
                    {
                        await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                        var yieldTask = yield(i, "foo", new List<int>());
                        // tasks.Add(yieldTask);
                        await yieldTask;
                    }

                    //tasks.Add(yield(110, "bar", new List<int>()));
                    //tasks.Add(yield(111, "food", new List<int>()));
                    //tasks.Add(yield(112, "barf", new List<int>()));

                    await yield(110, "bar", new List<int>());
                    await yield(111, "food", new List<int>());
                    await yield(112, "barf", new List<int>());

                    await Task.WhenAll(tasks.ToArray());
                });
            int count = 0;
            await items.ForAllAsync(
                async (a, b, c) =>
                {
                    await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                    count++;
                });
            Assert.AreEqual(103, count);
        }

        [TestMethod]
        public async Task EnumerableAsyncGerenicTests()
        {
            var rand = new Random();
            var items = EnumerableAsync.YieldAsync<TestDelegateAsync> (
                async (yield) =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                        var yieldTask = yield(i, "foo", new List<int>());
                        try
                        {
                            await yieldTask;
                        } catch(Exception ex)
                        {
                            throw ex;
                        }
                    }

                    await yield(110, "bar", new List<int>());
                    await yield(111, "food", new List<int>());
                    await yield(112, "barf", new List<int>());
                });

            int count = 0;
            await items.ForAllAsync(
                async (a, b, c) =>
                {
                    await Task.Run(() => Thread.Sleep(rand.Next() % 30));
                    await Task.FromResult(true);
                    count++;
                });
            Assert.AreEqual(103, count);
        }

        [TestMethod]
        public async Task EnumerableAsyncBrakedTests()
        {
            var rand = new Random();
            var yieldCount = 0;
            var items = EnumerableAsync.YieldAsync<TestDelegateAsync>(
                async (yield) =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        yieldCount++;
                        await yield(i, "foo", new List<int>());
                    }
                });

            int count = 0;
            var culledItems = items.TakeAsync(50);
            await culledItems.ForAllAsync(
                async (a, b, c) =>
                {
                    await Task.Run(() => Thread.Sleep(rand.Next() % 20));
                    count++;
                });
            Assert.AreEqual(50, count);
            Assert.AreEqual(51, yieldCount);
        }

        [TestMethod]
        [Ignore]
        public async Task EnumerableAsyncNotAsyncTests()
        {
            var items = EnumerableAsync.YieldAsync<TestDelegate>(
                async (yield) =>
                {
                    await Task.FromResult(true);
                    yield(112, "barf", new List<int>());
                });

            try
            {
                await items.ForAllAsync(
                    async (a, b, c) => await Task.FromResult(true));
                Assert.Fail("Cannot have non-async IEnumerableAsync delegates");
            } catch(ArgumentException ex)
            {
                Assert.AreEqual(typeof(TestDelegate).FullName, ex.ParamName);
            }
        }
    }
}

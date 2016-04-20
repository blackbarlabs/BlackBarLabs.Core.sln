using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlackBarLabs.Collections.Async;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace BlackBarLabs.Core.Tests
{
    [TestClass]
    public class EnumerableAsyncTests
    {
        [TestMethod]
        public async Task PrespoolAllowsParallel()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var taskToPrespool = EnumerableAsync.YieldAsync<Func<int, Task>>(
                async (yieldAsync) =>
                {
                    var tasks = Enumerable.Range(0, 100).Select(async (i) =>
                    {
                        await Task.Run(() => Thread.Sleep(1000));
                        await yieldAsync(i);
                    });
                    await Task.WhenAll(tasks);
                    return;
                });

            // Test fails without this line
            taskToPrespool = taskToPrespool.PrespoolAsync();
            
            int total = 0;
            await taskToPrespool.ForAllAsync(
                async (i) =>
                {
                    await Task.FromResult(false);
                    total += i;
                });
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 25000);
            Assert.AreEqual(4950, total);
        }

        [TestMethod]
        public async Task PrespoolMaintainOrder()
        {
            var stopwatch = new Stopwatch();
            var taskToPrespool = EnumerableAsync.YieldAsync<Func<int, Task>>(
                async (yieldAsync) =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        await Task.Run(() => Thread.Sleep(10));
                        await yieldAsync(i);
                    }
                    return;
                });

            // Test fails without this line
            taskToPrespool = taskToPrespool.PrespoolAsync();

            await Task.Run(() => Thread.Sleep(1000));
            stopwatch.Start();
            int index = -1;
            await taskToPrespool.ForAllAsync(
                async (i) =>
                {
                    await Task.FromResult(false);
                    Assert.IsTrue(index < i);
                    index = i;
                });
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500);
        }
    }
}

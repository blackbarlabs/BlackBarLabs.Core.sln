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
    [TestClass]
    public class ViewBizPersistenceTests
    {
        public class MyApiResource
        {
            public Guid ParentId { get; set; }
            public int A { get; set; }
            public string B { get; set; }
            public object C { get; set; }
        }

        public delegate Task BizObjectInfoDelegateAsync(int a, string b, object c);

        public delegate Task StorageDelegateAsync(int a, string b);

        [TestMethod]
        public void SimulateApiFetch()
        {
            var request = new MyApiResource() { ParentId = Guid.NewGuid() };
            var items = this.GetBizObject(request.ParentId);
            var results = items.ToEnumerable(
                (int a, string b, object c) =>
                {
                    return new MyApiResource()
                    {
                        A = a,
                        B = b,
                        C = c,
                        ParentId = request.ParentId,
                    };
                });

            var resultsArray = results.ToArray();
            Assert.AreEqual(82, resultsArray.Count());
        }

        private IEnumerableAsync<BizObjectInfoDelegateAsync> GetBizObject(Guid parentId)
        {
            var itemsInStorage = FindByParentId(parentId);
            var items = EnumerableAsync.YieldAsync<BizObjectInfoDelegateAsync>(
                async (yield) =>
                {
                    await itemsInStorage.ForAllAsync(async (a, b) =>
                    {
                        if (a % 5 == 0)
                            return;
                        await yield(a, b, new object());
                    });
                });

            return items;
        }
        
        private IEnumerableAsync<StorageDelegateAsync> FindByParentId(Guid parentId)
        {
            var items = EnumerableAsync.YieldAsync<StorageDelegateAsync>(
                async (yield) =>
                {
                    var tasks = new List<Task>();
                    for (int i = 0; i < 100; i++)
                    {
                        await yield(i, "foo");
                    }

                    await yield(110, "bar");
                    await yield(111, "food");
                    await yield(112, "barf");
                });

            return items;
        }

        /// <summary>
        /// Make sure this catches exceptions thrown from business layer
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task SimulateNotFound()
        {
            var request = new MyApiResource() { ParentId = Guid.NewGuid() };
            try
            {
                var items = await this.GetBizObjectSomeNotFound(request.ParentId);
                var results = items.ToEnumerable(
                    (int a, string b, object c) =>
                    {
                        return new MyApiResource()
                        {
                            A = a,
                            B = b,
                            C = c,
                            ParentId = request.ParentId,
                        };
                    });
                var resultsArray = results.ToArray();
                Assert.Fail();
            } catch(Exception ex)
            {
                Assert.AreEqual("NotFoundBiz", ex.Message);
            }
        }
        
        private async Task<IEnumerableAsync<BizObjectInfoDelegateAsync>> GetBizObjectSomeNotFound(Guid parentId)
        {
            try
            {
                var itemsInStorage = FindByParentIdSomeNotFound(parentId);
                await itemsInStorage.ForAllAsync((a, b) => Task.FromResult(true));
                Assert.Fail();
            } catch(Exception ex)
            {
                Assert.AreEqual("NotFoundStorage", ex.Message);
            }
            var items = EnumerableAsync.YieldAsync<BizObjectInfoDelegateAsync>(
                async (yield) =>
                {
                    await Task.FromResult(true);
                    throw new Exception("NotFoundBiz");
                });

            return items;
        }

        private IEnumerableAsync<StorageDelegateAsync> FindByParentIdSomeNotFound(Guid parentId)
        {
            var items = EnumerableAsync.YieldAsync<StorageDelegateAsync>(
                async (yield) =>
                {
                    await Task.FromResult(true);
                    throw new Exception("NotFoundStorage");
                });

            return items;
        }
    }
}

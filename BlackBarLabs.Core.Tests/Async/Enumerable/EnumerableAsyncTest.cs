using BlackBarLabs.Collections.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Core.Tests
{
    public class EnumerableAsyncTest : IEnumerableAsync<TestDelegateAsync>
    {
        public static IEnumerableAsync<TestDelegateAsync> YieldAsync(YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            return new EnumerableAsyncTest(yieldAsync);
        }

        private YieldCallbackAsync<TestDelegateAsync> yieldAsync;

        public EnumerableAsyncTest(YieldCallbackAsync<TestDelegateAsync> yieldAsync)
        {
            this.yieldAsync = yieldAsync;
        }

        public IEnumeratorAsync<TestDelegateAsync> GetEnumerator()
        {
            return new EnumeratorAsyncTest(this.yieldAsync);
        }

        public IEnumerable<int> GetEnumerable(TestDelegateResult<int> convertDelegate)
        {
            return new EnumerableTest(this.yieldAsync, convertDelegate);
        }

        public IEnumerable<TResult> GetEnumerable<TResult, TConvertDelegate>(TConvertDelegate convertDelegate)
        {
            throw new NotImplementedException();
        }

        public IIteratorAsync<TestDelegateAsync> GetIterator()
        {
            return new IteratorAsyncTest<TestDelegateAsync>(this.yieldAsync);
        }
    }
}

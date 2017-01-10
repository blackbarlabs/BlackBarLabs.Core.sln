using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Collections.Async
{
    public delegate Task YieldCallbackAsync<TDelegate>(TDelegate yield);
    public delegate Task YieldStructDelegateAsync<TStruct>(Func<TStruct, Task> yield);

    public class EnumerableAsync
    {
        public static IEnumerableAsync<TDelegate> YieldAsync<TDelegate>(YieldCallbackAsync<TDelegate> yieldAsync)
        {
            return new EnumerableAsync<TDelegate>(yieldAsync);
        }

        public static IEnumerableStructAsync<TStruct> YieldStructAsync<TStruct>(YieldStructDelegateAsync<TStruct> yieldAsync)
        {
            return new EnumerableStructAsync<TStruct>(yieldAsync);
        }
    }

    internal class EnumerableAsync<TDelegate> : IEnumerableAsync<TDelegate>
    {
        private YieldCallbackAsync<TDelegate> yieldAsync;

        public EnumerableAsync(YieldCallbackAsync<TDelegate> yieldAsync)
        {
            this.yieldAsync = yieldAsync;
        }

        public IEnumerable<TResult> GetEnumerable<TResult, TDelegateConvert>(TDelegateConvert convertDelegate)
        {
            return new EnumerableNonAsync<TResult, TDelegate, TDelegateConvert>(this.yieldAsync, convertDelegate);
        }

        public IEnumeratorAsync<TDelegate> GetEnumerator()
        {
            return new EnumeratorBlockingAsync<TDelegate>(this.yieldAsync);
        }

        public IIteratorAsync<TDelegate> GetIterator()
        {
            return new IteratorSimpleAsync<TDelegate>(this.yieldAsync);
        }
    }
    
    internal class EnumerableStructAsync<TStruct> : IEnumerableStructAsync<TStruct>
    {
        private YieldStructDelegateAsync<TStruct> yieldAsync;

        public EnumerableStructAsync(YieldStructDelegateAsync<TStruct> yieldAsync)
        {
            this.yieldAsync = yieldAsync;
        }
        
        public IEnumerable<TStruct> GetEnumerable()
        {
            return new EnumerableStructNonAsync<TStruct>(this.yieldAsync);
        }

        public IEnumeratorStructAsync<TStruct> GetEnumerator()
        {
            return new EnumeratorBlockingStructAsync<TStruct>(this.yieldAsync);
        }
    }
}

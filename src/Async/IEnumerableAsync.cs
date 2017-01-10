using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Collections.Async
{
    public interface IEnumerableAsync<TDelegate>
    {
        IEnumerable<TResult> GetEnumerable<TResult, TConvertDelegate>(TConvertDelegate convertDelegate);

        IEnumeratorAsync<TDelegate> GetEnumerator();

        IIteratorAsync<TDelegate> GetIterator();
    }

    public interface IEnumerableStructAsync<TStruct>
    {
        IEnumerable<TStruct> GetEnumerable();

        IEnumeratorStructAsync<TStruct> GetEnumerator();
    }
}

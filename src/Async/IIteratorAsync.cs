using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Collections.Async
{
    public interface IIteratorAsync<TDelegate>
    {
        Task IterateAsync(TDelegate callback);
    }
}

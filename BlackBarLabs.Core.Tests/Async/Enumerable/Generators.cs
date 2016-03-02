using BlackBarLabs.Collections.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Core.Tests
{
    internal static class Generators
    {
        public static Func<Func<TestDelegateAsync>, YieldCallbackAsync<TestDelegateAsync>, Func<Task, Task>, Task> GetSandwichDelegate()
        {
            Func<Func<TestDelegateAsync>, YieldCallbackAsync<TestDelegateAsync>, Func<Task, Task>, Task> sandwichDelegate =
                (preCallback, yieldA, postCallback) =>
                {
                    TestDelegateAsync invoked = (a, b, c) =>
                    {
                        var pre = preCallback.Invoke();
                        var callbackTask = pre.Invoke(a, b, c);
                        var delegateTask = postCallback.Invoke(callbackTask);
                        return delegateTask;
                    };
                    var yieldTask = yieldA.Invoke(invoked);
                    return yieldTask;
                };
            return sandwichDelegate;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Security.Cryptography;

namespace BlackBarLabs.Collections.Async
{
    public static class LinqExtensions
    {
        public static async Task ForAllAsync<T>(this IEnumerableAsync<T> items, T action)
        {
            var iterator = items.GetIterator();
            await iterator.IterateAsync(action);
            // while (await enumerator.MoveNextAsync(action)) { };
        }

        
        private static Func<ConcurrentQueue<Func<TDelegate, Task>>, TDelegate> GetTotalExpression<TDelegate>()
        {
            var queueExpression = Expression.Parameter(typeof(ConcurrentQueue<Func<TDelegate, Task>>), "queue");
            var delegateExpression = GetInvokedExpression<TDelegate>(queueExpression);

            var returnTarget = Expression.Label(typeof(TDelegate));
            var returnTaskExpression = Expression.Return(returnTarget, delegateExpression);
            var returnValue = default(TDelegate);
            var returnLabelExpression = Expression.Label(returnTarget, Expression.Constant(returnValue, typeof(TDelegate)));

            // Func<TDelegate, Task> invoked = (method) =>
            var bodyExpression = new Expression[]
            {
                returnTaskExpression,
                returnLabelExpression,
            };
            var blockWithReturnExpression = Expression.Block(bodyExpression);
            var invokedExpression = Expression.Lambda<Func<ConcurrentQueue<Func<TDelegate, Task>>, TDelegate>>(
                blockWithReturnExpression, queueExpression);
            return invokedExpression.Compile();
        }

        // (a, b, c) =>
        // {
        //     Func<T, Task> callback = async (method) =>
        //     {
        //         await method(a, b, c);
        //     };
        //     queue.Enqueue(callback);
        // }
        private static Expression<TDelegate> GetInvokedExpression<TDelegate>(ParameterExpression queueExpression)
        {
            var delegateInvokeMethod = typeof(TDelegate).GetMethod("Invoke");
            if (typeof(Task) != delegateInvokeMethod.ReturnType)
                throw new ArgumentException(
                    "Async Enumeration requires method that returns Task",
                    typeof(TDelegate).FullName);
            
            var delegateParameters = delegateInvokeMethod.GetParameters()
                .Select(param => Expression.Parameter(param.ParameterType))
                .ToArray();

            var callbackExpression = GetCallback<TDelegate>(delegateInvokeMethod, delegateParameters);
            var enqueueMethod = typeof(ConcurrentQueue<Func<TDelegate, Task>>).GetMethod("Enqueue");

            var queueEnqueueExpression = Expression.Call(queueExpression, enqueueMethod, callbackExpression);
            
            var trueExpression = Expression.Constant(true, typeof(bool));
            var defaultTaskExpression = Expression.Call(typeof(Task), "FromResult", new Type[] { typeof(bool) }, trueExpression);

            // return delegateTask;
            var returnTarget = Expression.Label(typeof(Task));
            var returnTaskExpression = Expression.Return(returnTarget, defaultTaskExpression);
            var returnValue = default(Task);
            var returnLabelExpression = Expression.Label(returnTarget, Expression.Constant(returnValue, typeof(Task)));

            // Func<TDelegate, Task> invoked = (method) =>
            var bodyExpression = new Expression[]
            {
                queueEnqueueExpression,
                returnTaskExpression,
                returnLabelExpression,
            };
            var blockWithReturnExpression = Expression.Block(bodyExpression);
            var invokedExpression = Expression.Lambda<TDelegate>(blockWithReturnExpression, delegateParameters);
            return invokedExpression;
        }

        private static Expression GetCallback<TDelegate>(
            System.Reflection.MethodInfo delegateInvokeMethod,
            ParameterExpression [] delegateParameters)
        {
            var methodExpression = Expression.Parameter(typeof(TDelegate), "method");
            
            var methodTaskExpression = Expression.Call(methodExpression, delegateInvokeMethod, delegateParameters);

            // return delegateTask;
            var returnTarget = Expression.Label(typeof(Task));
            var returnTaskExpression = Expression.Return(returnTarget, methodTaskExpression);
            var returnValue = default(Task);
            var returnLabelExpression = Expression.Label(returnTarget, Expression.Constant(returnValue, typeof(Task)));

            // Func<T, Task> callback = async (method) =>
            var callbackExpression = new Expression[]
            {
                returnTaskExpression,
                returnLabelExpression,
            };

            var blockWithReturnExpression = Expression.Block(callbackExpression);
            var invokedExpression = Expression.Lambda<Func<TDelegate, Task>>(blockWithReturnExpression, methodExpression);
            return invokedExpression;
        }

        public static IEnumerableAsync<T> PrespoolAsync<T>(this IEnumerableAsync<T> items)
        {
            var iterator = items.GetIterator();
            var queue = new ConcurrentQueue<Func<T, Task>>();
            var totalExpression = GetTotalExpression<T>();
            var basicExpression = totalExpression(queue);
            var iterationTask = iterator.IterateAsync(basicExpression);
            return EnumerableAsync.YieldAsync<T>(
                async (yieldAsync) =>
                {
                    while(!iterationTask.IsCompleted ||
                        queue.Count() > 0)
                    {
                        Func<T, Task> callback;
                        if (queue.TryDequeue(out callback))
                            await callback(yieldAsync);
                    }
                });
        }

        public static async Task<bool> FirstAsync<T>(this IEnumerableAsync<T> items, T action)
        {
            var enumerator = items.GetEnumerator();
            return await enumerator.MoveNextAsync(action);
        }

        public static IEnumerableAsync<T> TakeAsync<T>(this IEnumerableAsync<T> items, int limit)
        {
            YieldCallbackAsync<T> yieldAsync = async yield =>
            {
                using (var enumerator = items.GetEnumerator())
                {
                    while (limit > 0 && await enumerator.MoveNextAsync(yield))
                    {
                        limit--;
                    }
                }
            };
            return new EnumerableAsync<T>(yieldAsync);
        }

        public async static Task ForYield<T>(this IEnumerable<T> items, Func<T, Task> yieldAsync)
        {
            foreach(var item in items)
            {
                await yieldAsync.Invoke(item);
            }
        }

        public static IEnumerableAsync<Func<T, Task>> AsEnumerableAsync<T>(this IEnumerable<T> items)
        {
            return EnumerableAsync.YieldAsync<Func<T, Task>>(
                async (yieldAsync) =>
                {
                    await items.ForYield(yieldAsync);
                });
        }

        #region ToDictionary

        //public delegate void ToDictionaryDelegate<TKey, TValue>(TKey key, TValue value);
        //public static IDictionary<TKey, TValue> ToEnumerable<TDelegateItems, T1, T2, T3, TKey, TValue>(
        //    this IEnumerableAsync<TDelegateItems> items,
        //    Action<T1, T2, T3, ToDictionaryDelegate<TKey, TValue>> convert)
        //{
        //    var iterator = items.GetEnumerable<KeyValuePair<TKey, TValue>, Func<T1, T2, T3, KeyValuePair<TKey, TValue>>>(
        //        (t1, t2, t3) =>
        //        {
        //            var key = default(TKey);
        //            var value = default(TValue);
        //            convert(t1, t2, t3, (k, v) => { key = k; value = v; });
        //            return new KeyValuePair<TKey, TValue>(key, value);
        //        });
        //    return iterator.ToDictionary(;
        //}

        //public static IDictionary<TKey, TValue> ToDictionary<TDelegateItems, T1, TKey, TValue>(
        //    this IEnumerableAsync<TDelegateItems> items,
        //    Action<T1, ToDictionaryDelegate<TKey, TValue>> convert)
        //{
        //    var iterator = items.GetEnumerable<TResult, Func<T1, TResult>>(convert);
        //    return iterator;
        //}

        #endregion

        #region ToEnumerable

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, TDelegateConvert, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            TDelegateConvert convert)
        {
            var iterator = items.GetEnumerable<TResult, TDelegateConvert>(convert);
            return iterator;
        }
        
        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, T7, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, TResult>(
         this IEnumerableAsync<TDelegateItems> items,
         Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>(
            this IEnumerableAsync<TDelegateItems> items,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>(
           this IEnumerableAsync<TDelegateItems> items,
           Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, TResult>>(convert);
            return iterator;
        }


        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(
           this IEnumerableAsync<TDelegateItems> items,
           Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, TResult>(
          this IEnumerableAsync<TDelegateItems> items,
          Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, TResult>>(convert);
            return iterator;
        }

        public static IEnumerable<TResult> ToEnumerable<TDelegateItems, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, TResult>(
         this IEnumerableAsync<TDelegateItems> items,
         Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, TResult> convert)
        {
            var iterator = items.GetEnumerable<TResult, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, T17, T18, TResult>>(convert);
            return iterator;
        }

        #endregion

    }
}

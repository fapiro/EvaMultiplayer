using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Game;

namespace Core.Helpers.AsyncUtils
{
    public static class AsyncHelper
    {
        public static async Task RunAsync<TPar>(Action<TPar> action, TPar par)
        {
            var task = Task.Run(() => action?.Invoke(par));
            await task;
        }
        
        public static async Task<TResult> RunAsync<TPar, TResult>(Func<TPar, TResult> action, TPar par)
            where TResult : class
        {
            var task = Task.Run(() => action?.Invoke(par));
            var result = await task;
            return result;
        }
        
        public static async Task<TResult> GetActionAsync<TResult, TResponse>(
            Action<Func<TResponse, TResult>, Func<TResponse, TResult>> method,
            Func<TResponse, TResult> onSuccess,
            Func<TResponse, TResult> onError,
            CancellationToken cancellationToken = default,
            int delay = 10
        ) where TResult : class
        {
            var isFinished = false;

            TResult result = default;

            method(response =>
                {
                    result = onSuccess(response);
                    isFinished = true;
                    return result;
                },
                response =>
                {
                    result = onError(response);
                    isFinished = true;
                    return result;
                });

            while (!isFinished &&
                   (
                       cancellationToken == default ||
                       (cancellationToken != default && !cancellationToken.IsCancellationRequested)
                   )
            )
            {
                await Root.Delay(delay, cancellationToken);
            }

            return result;
        }

        public static async Task<TResult> GetCoroutineAsync<TResult, TResponse>(
            Func<Func<TResponse, TResult>, Func<TResponse, TResult>, IEnumerator> coroutine,
            Func<TResponse, TResult> onSuccess,
            Func<TResponse, TResult> onError,
            CancellationToken cancellationToken,
            int delay = 10
        ) where TResult : new()
        {
            var isFinished = false;

            TResult result = default;

            MonoHelper.Instance.StartCoroutine(coroutine(response =>
                {
                    if (!onSuccess.IsNullOrDead())
                        result = onSuccess.Invoke(response);
                    
                    isFinished = true;
                    return result;
                },
                response =>
                {
                    if (!onError.IsNullOrDead())
                        result = onError(response);
                    
                    isFinished = true;
                    return result;
                }));

            while (!isFinished && !cancellationToken.IsCancellationRequested)
            {
                await Root.Delay(delay, cancellationToken);
            }

            return result;
        }

        public static void RunSync(Func<Task> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            synch.Post(async _ =>
            {
                try
                {
                    await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();

            SynchronizationContext.SetSynchronizationContext(oldContext);
        }

        public static T RunSync<T>(Func<Task<T>> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            var ret = default(T);
            synch.Post(async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            return ret;
        }
    }
}

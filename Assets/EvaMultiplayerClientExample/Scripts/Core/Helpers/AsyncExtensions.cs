using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Core.Helpers.AsyncUtils
{
    public static class AsyncExtensions
    {
        private const int MINIMUM_TASK_DELAY_MILLISECONDS = 5;
        
        /// <summary>
        /// Helper to call async from sync
        /// </summary>
        /// <param name="task"></param>
        public static async void CallSyncNoWait(this Task task)
        {
            await task;
        }
        
        //do not use this generic method, because it is not tested
        /*public static async Task<T> CallAsyncFromSync<T>(this Task<T> task)
        {
            return await task;
        }*/
        
        /// <summary>
        /// Converts Task to Coroutine
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static IEnumerator AsIEnumerator(this Task task)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                ExceptionDispatchInfo.Capture(task.Exception).Throw();
        }

        /// <summary>
        /// Converts Task to Coroutine
        /// </summary>
        /// <param name="task"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerator<T> AsIEnumerator<T>(this Task<T> task)
        {
            while (!task.IsCompleted)
                yield return default;

            if (task.IsFaulted)
                ExceptionDispatchInfo.Capture(task.Exception).Throw();

            yield return task.Result;
        }
        
        public static async Task<T> ToTask<T>(this AsyncOperationHandle<T> handle)
        {
            return await handle.Task;
        }
     
        public static async Task<object> ToTask(this AsyncOperationHandle handle)
        {
            return await handle.Task;
        }
        
        public static TaskAwaiter<int> GetAwaiter(this Process process)
        {
            var taskCompletionSource = new TaskCompletionSource<int>();
            process.EnableRaisingEvents = true;

            process.Exited += (s, e) => taskCompletionSource.TrySetResult(process.ExitCode);

            if (process.HasExited)
                taskCompletionSource.TrySetResult(process.ExitCode);

            return taskCompletionSource.Task.GetAwaiter();
        }
    }
}

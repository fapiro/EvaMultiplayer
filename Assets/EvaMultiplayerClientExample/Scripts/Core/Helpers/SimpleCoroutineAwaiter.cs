using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Core.Helpers.AsyncUtils
{
    public class SimpleCoroutineAwaiter : INotifyCompletion
    {
        bool _isDone;
        Exception _exception;
        Action _continuation;

        public bool IsCompleted
        {
            get { return _isDone; }
        }

        public void GetResult()
        {
            Assert(_isDone);

            if (_exception != null)
            {
                ExceptionDispatchInfo.Capture(_exception).Throw();
            }
        }

        public void Complete(Exception e)
        {
            Assert(!_isDone);

            _isDone = true;
            _exception = e;

            // Always trigger the continuation on the unity thread when awaiting on unity yield
            // instructions
            if (_continuation != null)
            {
                CoroutineExtensions.RunOnUnityScheduler(_continuation);
            }
        }

        void INotifyCompletion.OnCompleted(Action continuation)
        {
            Assert(_continuation == null);
            Assert(!_isDone);

            _continuation = continuation;
        }
        
        private static void Assert(bool condition)
        {
            if (!condition)
                throw new Exception("Assert hit!");
        }
    }
    
    public class SimpleCoroutineAwaiter<T> : INotifyCompletion
    {
        bool _isDone;
        Exception _exception;
        Action _continuation;
        T _result;

        public bool IsCompleted
        {
            get { return _isDone; }
        }

        public T GetResult()
        {
            Assert(_isDone);

            if (_exception != null)
                ExceptionDispatchInfo.Capture(_exception).Throw();

            return _result;
        }

        public void Complete(T result, Exception e)
        {
            Assert(!_isDone);

            _isDone = true;
            _exception = e;
            _result = result;

            // Always trigger the continuation on the unity thread when awaiting on unity yield instructions
            if (_continuation != null)
                CoroutineExtensions.RunOnUnityScheduler(_continuation);
        }

        void INotifyCompletion.OnCompleted(Action continuation)
        {
            Assert(_continuation == null);
            Assert(!_isDone);

            _continuation = continuation;
        }
        
        private static void Assert(bool condition)
        {
            if (!condition)
                throw new Exception("Assert hit!");
        }
    }
}

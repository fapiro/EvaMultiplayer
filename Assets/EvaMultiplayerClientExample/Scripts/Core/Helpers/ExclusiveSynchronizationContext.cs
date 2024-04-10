using System;
using System.Collections.Generic;
using System.Threading;
using Core.Logger;

namespace Core.Helpers.AsyncUtils
{
    public class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private readonly AutoResetEvent _workItemsWaiting = new AutoResetEvent(false);
            
            private readonly Queue<Tuple<SendOrPostCallback, object>> _items =
                new Queue<Tuple<SendOrPostCallback, object>>();
            
            private bool _done;
            public Exception InnerException { get; set; }

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException("We cannot send to our same thread");
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (_items)
                {
                    _items.Enqueue(Tuple.Create(d, state));
                }

                _workItemsWaiting.Set();
            }

            public void EndMessageLoop()
            {
                Post(_ => _done = true, null);
            }

            public void BeginMessageLoop()
            {
                while (!_done)
                {
                    Tuple<SendOrPostCallback, object> task = null;
                    lock (_items)
                    {
                        if (_items.Count > 0)
                        {
                            task = _items.Dequeue();
                        }
                    }

                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null)
                        {
                            var errorMessage = $"AsyncHelper.RunSync method threw an exception";
                            Log.Error($"{errorMessage}");
                            throw new AggregateException(errorMessage, InnerException);
                        }
                    }
                    else
                        _workItemsWaiting.WaitOne();
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }
        }
}

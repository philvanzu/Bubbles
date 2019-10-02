using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bubbles3.Utils
{
    public class BblTaskScheduler:TaskScheduler
    {
        ConcurrentPriorityQueue<BblTask> _tasks = new ConcurrentPriorityQueue<BblTask>();
        
        // Indicates whether the scheduler is currently processing work items. 
        bool _currentThreadIsProcessingItems;
        private int _delegatesQueuedOrRunning = 0;
        
        // The maximum concurrency level allowed by this scheduler. 
        private readonly int _maxDegreeOfParallelism;


        public BblTaskScheduler(int maxDegreeOfParallelism)
        {
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks;
        }

        protected override void QueueTask(Task task)
        {
            BblTask t = task as BblTask;
            if (t == null) throw new ArgumentException("The BblTaskScheduler only processes BblTasks");
            _tasks.Enqueue(t);
            if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
            {
                ++_delegatesQueuedOrRunning;
                NotifyThreadPoolOfPendingWork();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item = null;
                        // When there are no more items to be processed,
                        // note that we're done processing, and get out.
                        if (_tasks.Count == 0)
                        {
                            --_delegatesQueuedOrRunning;
                            break;
                        }

                        // Get the next item from the queue
                            
                        _tasks.TryDequeue(out BblTask t);
                        if (t != null) item = t;

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

    }
}

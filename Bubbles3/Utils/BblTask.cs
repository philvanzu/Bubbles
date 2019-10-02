using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using System.Threading;
namespace Bubbles3.Utils
{
    public static class BblTask
    {
        
        static QueuedTaskScheduler _queuedScheduler = new QueuedTaskScheduler(targetScheduler: TaskScheduler.Default, maxConcurrencyLevel: 4);
        //Three levels of priority for book loading tasks
        static TaskScheduler _ts_pri0;// Current image bitmap
        static TaskScheduler _ts_pri1;// Selected book population and thumbnails
        static TaskScheduler _ts_pri2;// default priority.
        static BblTask()
        {
            _ts_pri0 = _queuedScheduler.ActivateNewQueue(0);
            _ts_pri1 = _queuedScheduler.ActivateNewQueue(1);
            _ts_pri2 = _queuedScheduler.ActivateNewQueue(2);
        }



        public static Task Run(Action f, int priority, CancellationToken token, TaskCreationOptions options = TaskCreationOptions.HideScheduler | TaskCreationOptions.DenyChildAttach)
        {
            TaskScheduler ts = null;
            switch (priority)
            {
                case 0: ts = _ts_pri0; break;
                case 1: ts = _ts_pri1; break;
                case 2: ts = _ts_pri2; break;
            }
            return Task.Factory.StartNew(f, token, options, ts);
        }

        public static Task Run(Func<Task> f, int priority, CancellationToken token, TaskCreationOptions options = TaskCreationOptions.HideScheduler | TaskCreationOptions.DenyChildAttach)
        {
            TaskScheduler ts = null;
            switch (priority)
            {
                case 0: ts = _ts_pri0; break;
                case 1: ts = _ts_pri1; break;
                case 2: ts = _ts_pri2; break;
            }
            return Task.Factory.StartNew(f, token, options, ts);
        }
    }
}

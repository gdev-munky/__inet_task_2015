using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VKFriendsSorter
{
    public class ConcurrentExecutor
    {
        private Random r = new Random();
        private List<MyThread> _threads = new List<MyThread>(1024);

        public void KillAll()
        {
            foreach (var t in _threads)
                t.KillNoWait();
            _threads.Clear();
        }

        public void AddWorkers(int count, bool cleanFirst = false)
        {
            if (cleanFirst)
            {
                KillAll();
            }
            for (var i = 0; i < count; ++i)
                _threads.Add(new MyThread());
        }

        public bool PushTask(Action a)
        {
            if (_threads.Count < 1) return false;
            var minTasks = int.MaxValue;
            var thread = _threads[r.Next(0, _threads.Count)];/*_threads.First();
            foreach (var t in _threads)
            {
                if (t.TasksLeft >= minTasks)
                    continue;
                thread = t;
                minTasks = t.TasksLeft;
            }*/
            thread.PushTask(a);
            return true;
        }
        public bool PushTaskNoInterrupt(Action a)
        {
            if (_threads.Count < 1) return false;
            var minTasks = int.MaxValue;
            var thread = _threads[r.Next(0, _threads.Count)];
            thread.PushTaskNoInterrupt(a);
            return true;
        }

        public void SignalAll()
        {
            foreach (var t in _threads)
            {
                t.Signal();
            }
        }

        public int TasksLeftToDo
        {
            get { return _threads.Sum(thread => thread.TasksLeft); }
        }
        public bool IsComplete
        {
            get { return _threads.All(thread => thread.IsComplete); }
        }
    }

    public class MyThread
    {
        private Queue<Action> _todo = new Queue<Action>();
        private int _total = 0;
        private int _done = 0;
        private Thread _thr;
        private bool _shouldShutDown = false;
        private bool _sleeps = false;

        private object _lock = new object();

        public bool IsComplete { get { return _done >= _total; } }
        public int TasksLeft { get { return _total - _done; } }

        public MyThread()
        {
            _thr = new Thread(Loop);
            _thr.Start();
        }

        ~MyThread() { KillNoWait(); }

        public void PushTask(Action a)
        {
            lock (_lock)
            {
                _todo.Enqueue(a);
                _total++;
                if (_sleeps)
                    _thr.Interrupt();
            }
        }
        public void PushTaskNoInterrupt(Action a)
        {
            lock (_lock)
            {
                _todo.Enqueue(a);
                _total++;
            }
        }

        public void Kill()
        {
            if (_thr == null)
                return;
            _shouldShutDown = true;
            _thr.Interrupt();
            _thr.Join();
            _thr = null;
        }
        public void KillNoWait()
        {
            if (_thr == null)
                return;
            _shouldShutDown = true;
            _thr.Interrupt();
            _thr = null;
        }
        protected Action PopTask()
        {
            lock (_lock)
            {
                return _todo.Count < 1 ? null : _todo.Dequeue();
            }
        }
        protected void ReportDoneTask()
        {
            lock (_lock)
            {
                _done++;
            }
        }
        private void Loop()
        {
            while (!_shouldShutDown)
            {
                _sleeps = false;
                var task = PopTask();
                while (task != null)
                {
                    task();
                    ReportDoneTask();
                    task = PopTask();
                }
                try { _sleeps = true; Thread.Sleep(int.MaxValue); }
                catch (ThreadInterruptedException) { }
                _sleeps = false;
            }
        }

        public void Signal()
        {
            if (_sleeps)
                _thr.Interrupt();
        }
    }
}

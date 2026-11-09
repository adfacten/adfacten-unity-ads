using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AdfactenAds.Internal
{
    /// <summary>
    /// Work posted from any thread and run on the Unity main thread by <see cref="AdfactenDispatcher"/>.
    /// Actions run in posting order; delayed actions run once their time has come.
    /// </summary>
    internal sealed class MainThreadQueue
    {
        private struct Item
        {
            public double Due;
            public long Seq;
            public Action Action;
        }

        private readonly object _lock = new object();
        private readonly List<Item> _items = new List<Item>();
        private readonly List<Item> _batch = new List<Item>();
        private readonly Func<double> _clock;
        private long _seq;

        /// <summary>Called with exceptions thrown by queued actions. They never stop the queue.</summary>
        public Action<Exception> OnError;

        public MainThreadQueue() : this(null) { }

        /// <param name="clock">Seconds from any fixed origin; a monotonic stopwatch by default.</param>
        public MainThreadQueue(Func<double> clock)
        {
            if (clock == null)
            {
                var sw = Stopwatch.StartNew();
                clock = () => sw.Elapsed.TotalSeconds;
            }
            _clock = clock;
        }

        public double Now { get { return _clock(); } }

        public int Count
        {
            get { lock (_lock) return _items.Count; }
        }

        /// <summary>Runs <paramref name="action"/> on the next drain. Safe from any thread.</summary>
        public void Post(Action action)
        {
            PostAt(double.NegativeInfinity, action);
        }

        /// <summary>Runs <paramref name="action"/> on the first drain at least <paramref name="seconds"/> from now.</summary>
        public void PostDelayed(double seconds, Action action)
        {
            PostAt(_clock() + Math.Max(0, seconds), action);
        }

        private void PostAt(double due, Action action)
        {
            if (action == null) return;
            lock (_lock)
            {
                _items.Add(new Item { Due = due, Seq = _seq++, Action = action });
            }
        }

        /// <summary>
        /// Runs every action that is due now, in order (by due time, then posting order). Actions posted while
        /// draining wait for the next drain. Returns the number of actions run.
        /// </summary>
        public int Drain()
        {
            var now = _clock();
            _batch.Clear();
            lock (_lock)
            {
                if (_items.Count == 0) return 0;
                for (var i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Due <= now)
                    {
                        _batch.Add(_items[i]);
                        _items.RemoveAt(i);
                    }
                }
            }
            if (_batch.Count == 0) return 0;
            _batch.Sort((a, b) => a.Due != b.Due ? a.Due.CompareTo(b.Due) : a.Seq.CompareTo(b.Seq));
            var run = _batch.ToArray();
            _batch.Clear();
            foreach (var item in run)
            {
                try
                {
                    item.Action();
                }
                catch (Exception e)
                {
                    var onError = OnError;
                    if (onError != null) onError(e);
                }
            }
            return run.Length;
        }

        public void Clear()
        {
            lock (_lock) _items.Clear();
        }
    }
}

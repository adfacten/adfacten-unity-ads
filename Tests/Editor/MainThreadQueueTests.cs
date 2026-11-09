using System;
using System.Collections.Generic;
using System.Threading;
using AdfactenAds.Internal;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class MainThreadQueueTests
    {
        private double _now;
        private MainThreadQueue _queue;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _queue = new MainThreadQueue(() => _now);
        }

        [Test]
        public void RunsInPostingOrder()
        {
            var order = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                var n = i;
                _queue.Post(() => order.Add(n));
            }
            Assert.AreEqual(0, order.Count, "nothing runs before a drain");
            Assert.AreEqual(5, _queue.Drain());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, order);
        }

        [Test]
        public void DelayedActionsWaitForTheirTime()
        {
            var order = new List<string>();
            _queue.PostDelayed(2, () => order.Add("late"));
            _queue.PostDelayed(1, () => order.Add("early"));
            _queue.Post(() => order.Add("now"));
            _queue.Drain();
            CollectionAssert.AreEqual(new[] { "now" }, order);
            _now = 1.5;
            _queue.Drain();
            CollectionAssert.AreEqual(new[] { "now", "early" }, order);
            _now = 5;
            _queue.Drain();
            CollectionAssert.AreEqual(new[] { "now", "early", "late" }, order);
        }

        [Test]
        public void ActionsPostedWhileDrainingWaitForTheNextDrain()
        {
            var count = 0;
            _queue.Post(() => { count++; _queue.Post(() => count++); });
            _queue.Drain();
            Assert.AreEqual(1, count);
            _queue.Drain();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ExceptionsDoNotStopTheQueue()
        {
            var errors = new List<Exception>();
            _queue.OnError = errors.Add;
            var ran = false;
            _queue.Post(() => { throw new InvalidOperationException("boom"); });
            _queue.Post(() => ran = true);
            _queue.Drain();
            Assert.IsTrue(ran);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void PostsFromManyThreadsRunOnTheDrainingThread()
        {
            var queue = new MainThreadQueue();
            const int threads = 8, perThread = 2000;
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            var ran = 0;
            var wrongThread = 0;
            var workers = new List<Thread>();
            for (var t = 0; t < threads; t++)
            {
                var w = new Thread(() =>
                {
                    for (var i = 0; i < perThread; i++)
                    {
                        queue.Post(() =>
                        {
                            ran++;
                            if (Thread.CurrentThread.ManagedThreadId != mainThread) wrongThread++;
                        });
                    }
                });
                workers.Add(w);
                w.Start();
            }
            // Drain while the workers are still posting, like Update() does every frame.
            while (workers.Exists(w => w.IsAlive)) queue.Drain();
            foreach (var w in workers) w.Join();
            queue.Drain();
            Assert.AreEqual(threads * perThread, ran);
            Assert.AreEqual(0, wrongThread);
        }
    }
}

using System.Collections.Generic;
using System.Threading;
using AdfactenAds.Internal;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    /// <summary>Bridge events arrive on Android's UI thread or iOS callbacks; they must reach C# on the main thread.</summary>
    public class MarshallingTests
    {
        private sealed class Recorder : IAdEventTarget
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<int> Threads = new List<int>();
            public Dictionary<string, object> LastPayload;

            public void HandleEvent(string name, Dictionary<string, object> payload)
            {
                Events.Add(name);
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                LastPayload = payload;
            }
        }

        [Test]
        public void EventsFromABackgroundThreadRunOnTheDrainingThread()
        {
            using (var env = new TestEnv())
            {
                var recorder = new Recorder();
                AdfactenRuntime.Router.Register(77, recorder);
                var t = new Thread(() =>
                {
                    AdfactenRuntime.PostEvent(77, "failed", "{\"name\":\"network_error\",\"message\":\"offline\",\"nativeCode\":3}");
                    AdfactenRuntime.PostEvent(77, "loaded", null);
                });
                t.Start();
                t.Join();
                Assert.IsEmpty(recorder.Events, "nothing runs on the posting thread");
                env.Drain();
                CollectionAssert.AreEqual(new[] { "failed", "loaded" }, recorder.Events);
                Assert.IsTrue(recorder.Threads.TrueForAll(id => id == Thread.CurrentThread.ManagedThreadId));
                Assert.AreEqual(0, recorder.LastPayload.Count);
            }
        }

        [Test]
        public void EventsForUnknownHandlesAreDropped()
        {
            using (var env = new TestEnv())
            {
                AdfactenRuntime.PostEvent(12345, "loaded", "{}");
                Assert.DoesNotThrow(() => env.Drain());
            }
        }

        [Test]
        public void FullScreenEventsFromTheBridgeAreParsed()
        {
            using (var env = new TestEnv())
            {
                RewardedAd ad = null;
                RewardedAd.Load("plc_r", (a, e) => ad = a);
                env.Advance(0.5);
                AdReward reward = null;
                AdError failed = null;
                ad.OnUserEarnedReward += r => reward = r;
                ad.OnFailedToShow += e => failed = e;

                // As the Android / iOS bridges send them.
                AdfactenRuntime.PostEvent(ad.Handle, "reward", "{\"type\":\"gems\",\"amount\":25}");
                AdfactenRuntime.PostEvent(ad.Handle, "failedToShow", "{\"name\":\"ad_expired\",\"message\":\"expired\",\"nativeCode\":8}");
                env.Drain();
                Assert.AreEqual("gems", reward.Type);
                Assert.AreEqual(25.0, reward.Amount);
                Assert.AreEqual(AdErrorCode.AdExpired, failed.Code);
                Assert.AreEqual(8, failed.NativeCode);
            }
        }

        [Test]
        public void LoadCallbackNeverRunsInsideLoad()
        {
            using (var env = new TestEnv())
            {
                var called = false;
                InterstitialAd.Load("", (a, e) => called = true);
                Assert.IsFalse(called);
                env.Drain();
                Assert.IsTrue(called);
            }
        }

        [Test]
        public void ExceptionsInGameCallbacksAreIsolated()
        {
            using (var env = new TestEnv())
            {
                var errors = new List<System.Exception>();
                env.Queue.OnError = errors.Add;
                var banner = new BannerView("plc_b", AdSize.Banner, AdPosition.Bottom);
                var shown = false;
                banner.OnLoaded += () => { throw new System.InvalidOperationException("game bug"); };
                banner.OnShown += () => shown = true;
                banner.Load();
                env.Advance(0.5);
                env.Advance(0.3);
                Assert.AreEqual(1, errors.Count);
                Assert.IsTrue(shown, "later events still arrive");
            }
        }
    }
}

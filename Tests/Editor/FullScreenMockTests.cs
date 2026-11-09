using System.Collections.Generic;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class FullScreenMockTests
    {
        [Test]
        public void InterstitialLoadShowClose()
        {
            using (var env = new TestEnv())
            {
                InterstitialAd ad = null;
                AdError error = null;
                InterstitialAd.Load("plc_i", (a, e) => { ad = a; error = e; });
                env.Drain();
                Assert.IsNull(ad, "not loaded before the mock delay");
                env.Advance(0.5);
                Assert.IsNull(error);
                Assert.IsNotNull(ad);
                Assert.AreEqual("plc_i", ad.PlacementId);
                Assert.IsTrue(ad.IsReady);

                var events = new List<string>();
                ad.OnShown += () => events.Add("shown");
                ad.OnClicked += () => events.Add("clicked");
                ad.OnClosed += () => events.Add("closed");
                ad.Show();
                Assert.IsFalse(ad.IsReady, "single use");
                env.Advance(0.2);
                env.Mock.ClickFullScreen(ad.Handle);
                env.Mock.CloseFullScreen(ad.Handle);
                env.Drain();
                CollectionAssert.AreEqual(new[] { "shown", "clicked", "closed" }, events);
                Assert.IsTrue(ad.IsDestroyed, "released after closing");
            }
        }

        [Test]
        public void ShowTwiceFailsWithAlreadyShown()
        {
            using (var env = new TestEnv())
            {
                var ad = LoadInterstitial(env);
                AdError failed = null;
                ad.OnFailedToShow += e => failed = e;
                ad.Show();
                ad.Show();
                env.Drain();
                Assert.IsNotNull(failed);
                Assert.AreEqual(AdErrorCode.AlreadyShown, failed.Code);
            }
        }

        [Test]
        public void SecondAdWhileOneIsOnScreenCanBeShownLater()
        {
            using (var env = new TestEnv())
            {
                var first = LoadInterstitial(env);
                var second = LoadInterstitial(env);
                AdError failed = null;
                var shown = false;
                second.OnFailedToShow += e => failed = e;
                second.OnShown += () => shown = true;
                first.Show();
                second.Show();
                env.Advance(0.2);
                Assert.AreEqual(AdErrorCode.ShowFailed, failed.Code);
                env.Mock.CloseFullScreen(first.Handle);
                env.Drain();
                Assert.IsTrue(second.IsReady, "show_failed leaves the ad usable");
                second.Show();
                env.Advance(0.2);
                Assert.IsTrue(shown);
            }
        }

        [Test]
        public void ShowAfterDestroyFailsWithNotReady()
        {
            using (var env = new TestEnv())
            {
                var ad = LoadInterstitial(env);
                AdError failed = null;
                ad.OnFailedToShow += e => failed = e;
                ad.Destroy();
                ad.Destroy();
                ad.Show();
                env.Drain();
                Assert.AreEqual(AdErrorCode.NotReady, failed.Code);
            }
        }

        [Test]
        public void RewardedGrantsTheRewardAfterCompletion()
        {
            using (var env = new TestEnv())
            {
                RewardedAd ad = null;
                RewardedAd.Load("plc_r", "level=3", (a, e) => ad = a);
                env.Advance(0.5);
                Assert.IsNotNull(ad);
                Assert.AreEqual("coins", ad.Reward.Type);
                Assert.AreEqual(10.0, ad.Reward.Amount);

                AdReward fromCallback = null, fromEvent = null;
                ad.OnUserEarnedReward += r => fromEvent = r;
                ad.Show(r => fromCallback = r);
                env.Advance(4.9);
                Assert.IsNull(fromCallback);
                env.Advance(0.2);
                Assert.IsNotNull(fromCallback);
                Assert.IsNotNull(fromEvent);
                Assert.AreEqual(10.0, fromCallback.Amount);
            }
        }

        [Test]
        public void ClosingRewardedEarlyForfeitsTheReward()
        {
            using (var env = new TestEnv())
            {
                RewardedAd ad = null;
                RewardedAd.Load("plc_r", (a, e) => ad = a);
                env.Advance(0.5);
                var rewarded = false;
                var closed = false;
                ad.OnUserEarnedReward += r => rewarded = true;
                ad.OnClosed += () => closed = true;
                ad.Show();
                env.Advance(1);
                env.Mock.CloseFullScreen(ad.Handle);
                env.Advance(10);
                Assert.IsTrue(closed);
                Assert.IsFalse(rewarded);
            }
        }

        [Test]
        public void LoadBeforeInitializeFails()
        {
            using (var env = new TestEnv(false))
            {
                AdError error = null;
                AppOpenAd.Load("plc_a", (a, e) => error = e);
                env.Advance(1);
                Assert.AreEqual(AdErrorCode.NotInitialized, error.Code);
            }
        }

        [Test]
        public void SimulatedLoadError()
        {
            using (var env = new TestEnv())
            {
                AdfactenMock.LoadError = AdErrorCode.NoFill;
                AppOpenAd ad = null;
                AdError error = null;
                AppOpenAd.Load("plc_a", (a, e) => { ad = a; error = e; });
                env.Advance(1);
                Assert.IsNull(ad);
                Assert.AreEqual(AdErrorCode.NoFill, error.Code);
                Assert.AreEqual("no_fill", error.Name);
            }
        }

        [Test]
        public void EmptyPlacementIsInvalidRequest()
        {
            using (var env = new TestEnv())
            {
                AdError error = null;
                RewardedAd.Load("  ", (a, e) => error = e);
                Assert.IsNull(error, "never inside the call");
                env.Drain();
                Assert.AreEqual(AdErrorCode.InvalidRequest, error.Code);
            }
        }

        private static InterstitialAd LoadInterstitial(TestEnv env)
        {
            InterstitialAd ad = null;
            InterstitialAd.Load("plc_i", (a, e) => ad = a);
            env.Advance(0.5);
            Assert.IsNotNull(ad);
            return ad;
        }
    }
}

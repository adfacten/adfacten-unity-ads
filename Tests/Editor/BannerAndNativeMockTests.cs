using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AdfactenAds.Tests
{
    public class BannerAndNativeMockTests
    {
        [Test]
        public void BannerLoadsThenRecordsAnImpression()
        {
            using (var env = new TestEnv())
            {
                var banner = new BannerView("plc_b", AdSize.MediumRectangle, AdPosition.Top);
                var events = new List<string>();
                banner.OnLoaded += () => events.Add("loaded");
                banner.OnShown += () => events.Add("shown");
                banner.Load();
                env.Advance(0.5);
                env.Advance(0.3);
                CollectionAssert.AreEqual(new[] { "loaded", "shown" }, events);
                Assert.AreEqual(300f, banner.GetWidthInPixels());
                Assert.AreEqual(250f, banner.GetHeightInPixels());
            }
        }

        [Test]
        public void HiddenBannerWaitsForShowBeforeTheImpression()
        {
            using (var env = new TestEnv())
            {
                var banner = new BannerView("plc_b", AdSize.Banner, AdPosition.Bottom);
                var shown = 0;
                banner.OnShown += () => shown++;
                banner.Hide();
                banner.Load();
                env.Advance(2);
                Assert.AreEqual(0, shown);
                Assert.IsFalse(banner.IsVisible);
                banner.Show();
                env.Advance(0.3);
                Assert.AreEqual(1, shown);
            }
        }

        [Test]
        public void DestroyedBannerSendsNoEvents()
        {
            using (var env = new TestEnv())
            {
                var banner = new BannerView("plc_b", AdSize.Banner, AdPosition.Bottom);
                var events = 0;
                banner.OnLoaded += () => events++;
                banner.Load();
                banner.Destroy();
                env.Advance(2);
                Assert.AreEqual(0, events);
                Assert.AreEqual(0f, banner.GetHeightInPixels());
                Assert.DoesNotThrow(() => banner.Destroy());
            }
        }

        [Test]
        public void BannerFailureCarriesTheError()
        {
            using (var env = new TestEnv())
            {
                AdfactenMock.LoadError = AdErrorCode.Rejected;
                var banner = new BannerView("plc_b", AdSize.Banner, AdPosition.Bottom);
                AdError error = null;
                banner.OnFailedToLoad += e => error = e;
                banner.Load();
                env.Advance(1);
                Assert.AreEqual(AdErrorCode.Rejected, error.Code);
            }
        }

        [Test]
        public void MockPlacesBannersLikeTheDevices()
        {
            using (var env = new TestEnv())
            {
                var banner = new BannerView("plc_b", AdSize.Banner, AdPosition.Bottom);
                banner.Load();
                env.Advance(1);
                foreach (var b in env.Mock.Banners)
                {
                    var r = env.Mock.BannerRect(b, 1000, 2000);
                    Assert.AreEqual(340f, r.x);
                    Assert.AreEqual(1950f, r.y);
                }
                banner.SetPosition(AdPosition.Custom(12, 34));
                foreach (var b in env.Mock.Banners)
                {
                    var r = env.Mock.BannerRect(b, 1000, 2000);
                    Assert.AreEqual(12f, r.x);
                    Assert.AreEqual(34f, r.y);
                }
            }
        }

        [Test]
        public void NativeLoadShowMoveAndDestroy()
        {
            using (var env = new TestEnv())
            {
                NativeAd ad = null;
                NativeAd.Load("plc_n", (a, e) => ad = a);
                env.Advance(0.5);
                Assert.IsNotNull(ad);
                Assert.AreEqual("Test native ad", ad.Title);
                Assert.AreEqual("Ad", ad.AdLabel);
                Assert.IsTrue(ad.HasImage);
                Assert.AreEqual(1200f, ad.ImageWidth);

                var shown = 0;
                var clicks = 0;
                ad.OnShown += () => shown++;
                ad.OnClicked += () => clicks++;
                ad.Show(NativeAdTemplate.Medium, new Rect(0, 100, 600, 500));
                env.Advance(0.2);
                ad.Show(NativeAdTemplate.Small, new Rect(0, 300, 600, 160));
                env.Advance(1);
                Assert.AreEqual(1, shown, "moving the template never records a second impression");
                foreach (var n in env.Mock.Natives)
                {
                    Assert.AreEqual(0, n.Template);
                    Assert.AreEqual(300f, n.Rect.y);
                    env.Mock.ClickNative(n.Handle);
                }
                env.Drain();
                Assert.AreEqual(1, clicks);

                ad.Hide();
                Assert.IsFalse(ad.IsVisible);
                ad.Destroy();
                Assert.IsTrue(ad.IsDestroyed);
                Assert.IsEmpty(new List<object>(env.Mock.Natives));
            }
        }

        [Test]
        public void NativeShowWithAnEmptyRectIsIgnored()
        {
            using (var env = new TestEnv())
            {
                NativeAd ad = null;
                NativeAd.Load("plc_n", (a, e) => ad = a);
                env.Advance(0.5);
                ad.Show(NativeAdTemplate.Small, new Rect(0, 0, 0, 0));
                Assert.IsFalse(ad.IsVisible);
                Assert.IsTrue(env.Logs.Exists(l => l.StartsWith("warning:")));
            }
        }

        [Test]
        public void NativeStyleColorsAreArgb()
        {
            Assert.AreEqual(0xFF1E88E5L, NativeAdStyle.ToArgb(new Color32(0x1E, 0x88, 0xE5, 0xFF)));
            Assert.AreEqual(0x80000000L, NativeAdStyle.ToArgb(new Color32(0, 0, 0, 0x80)));
            var json = new NativeAdStyle { TitleColor = new Color32(1, 2, 3, 255), CornerRadius = 8 }.ToJson();
            var map = AdfactenAds.Internal.FlatJson.Parse(json);
            Assert.AreEqual(2, map.Count, "only set values are sent");
            Assert.AreEqual(0xFF010203L, (long)AdfactenAds.Internal.FlatJson.GetDouble(map, "titleColor"));
            Assert.AreEqual(8.0, AdfactenAds.Internal.FlatJson.GetDouble(map, "cornerRadius"));
        }

        [Test]
        public void ScreenRectFromBottomLeft()
        {
            var r = AdfactenScreen.FromBottomLeft(new Rect(10, 100, 300, 50), 1000);
            Assert.AreEqual(10f, r.x);
            Assert.AreEqual(850f, r.y);
            Assert.AreEqual(300f, r.width);
            Assert.AreEqual(50f, r.height);
        }
    }
}

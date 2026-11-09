using System.Collections.Generic;
using UnityEngine;

namespace AdfactenAds.Internal
{
    /// <summary>
    /// Stand-in for the native SDK in the Editor (and other platforms in development builds). It sends no
    /// requests; it fires the same events as the real SDK with placeholder ads drawn by <see cref="MockOverlay"/>.
    /// </summary>
    internal sealed class MockClient : IAdfactenClient
    {
        internal sealed class Banner
        {
            public int Handle;
            public string PlacementId;
            public int SizeIndex;
            public int Position;
            public float X, Y;
            public bool Visible;
            public bool Loaded;
            public bool Impression;
        }

        internal sealed class FullScreen
        {
            public int Handle;
            public AdFormat Format;
            public string PlacementId;
            public bool Loaded;
            public bool ShowCalled;
            public bool Showing;
            public double ShowStartedAt;
            public bool Rewarded;
        }

        internal sealed class Native
        {
            public int Handle;
            public string PlacementId;
            public bool Loaded;
            public bool Visible;
            public int Template;
            public Rect Rect;
            public bool Impression;
        }

        internal const string RewardType = "coins";
        internal const double RewardAmount = 10;

        private static readonly int[,] BannerSizes = { { 320, 50 }, { 320, 100 }, { 300, 250 }, { 728, 90 } };

        private readonly MainThreadQueue _queue;
        private readonly Dictionary<int, Banner> _banners = new Dictionary<int, Banner>();
        private readonly Dictionary<int, FullScreen> _fullScreens = new Dictionary<int, FullScreen>();
        private readonly Dictionary<int, Native> _natives = new Dictionary<int, Native>();
        private bool _initialized;

        public MockClient(MainThreadQueue queue)
        {
            _queue = queue;
            PixelsPerDp = -1;
        }

        /// <summary>Calls received from the plugin, for tests: <c>setWrapper:unity:1.0.0</c>, <c>initialize:apk_…</c>, …</summary>
        internal readonly List<string> Calls = new List<string>();

        internal string WrapperName { get; private set; }
        internal string WrapperVersion { get; private set; }
        internal string AppKey { get; private set; }
        internal AdfactenConfig.Resolved Config { get; private set; }
        internal string UserId { get; private set; }

        /// <summary>Unity pixels per dp for placeholder sizes; derived from <c>Screen.dpi</c> unless set.</summary>
        internal float PixelsPerDp { get; set; }

        internal IEnumerable<Banner> Banners { get { return _banners.Values; } }
        internal IEnumerable<Native> Natives { get { return _natives.Values; } }

        internal FullScreen Showing
        {
            get
            {
                foreach (var f in _fullScreens.Values)
                {
                    if (f.Showing) return f;
                }
                return null;
            }
        }

        public string SdkVersion { get { return "mock"; } }
        public bool IsInitialized { get { return _initialized; } }

        public void SetWrapper(string name, string version)
        {
            Calls.Add("setWrapper:" + name + ":" + version);
            WrapperName = name;
            WrapperVersion = version;
        }

        public void Initialize(string appKey, AdfactenConfig.Resolved config)
        {
            Calls.Add("initialize:" + appKey);
            var first = !_initialized;
            _initialized = true;
            AppKey = appKey;
            Config = config;
            if (config.UserId != null) UserId = config.UserId;
            if (first)
            {
                AdLog.Info("Editor mock initialized (app key " + appKey + ", test mode " + (config.TestMode ? "on" : "off") +
                           "). Ads are placeholders and no requests are sent; build for Android or iOS to get real test ads.");
            }
        }

        public void SetUserId(string userId)
        {
            Calls.Add("setUserId:" + (userId ?? "null"));
            UserId = userId;
        }

        // Banner

        public void BannerCreate(int handle, string placementId, int sizeIndex)
        {
            _banners[handle] = new Banner { Handle = handle, PlacementId = placementId, SizeIndex = Mathf.Clamp(sizeIndex, 0, 3), Visible = true };
        }

        public void BannerSetPosition(int handle, int position, float x, float y)
        {
            Banner b;
            if (!_banners.TryGetValue(handle, out b)) return;
            b.Position = position;
            b.X = x;
            b.Y = y;
        }

        public void BannerLoad(int handle)
        {
            Banner b;
            if (!_banners.TryGetValue(handle, out b)) return;
            var error = LoadError();
            _queue.PostDelayed(AdfactenMock.LoadDelay, () =>
            {
                if (!_banners.ContainsKey(handle)) return;
                if (error != null)
                {
                    Emit(handle, "failed", error);
                    return;
                }
                b.Loaded = true;
                b.Impression = false;
                Emit(handle, "loaded", null);
                MaybeBannerImpression(b);
            });
        }

        public void BannerSetVisible(int handle, bool visible)
        {
            Banner b;
            if (!_banners.TryGetValue(handle, out b)) return;
            b.Visible = visible;
            MaybeBannerImpression(b);
        }

        public void BannerDestroy(int handle)
        {
            _banners.Remove(handle);
        }

        public bool BannerSizeInPixels(int handle, out float width, out float height)
        {
            Banner b;
            if (!_banners.TryGetValue(handle, out b))
            {
                width = height = 0;
                return false;
            }
            var scale = Scale();
            width = BannerSizes[b.SizeIndex, 0] * scale;
            height = BannerSizes[b.SizeIndex, 1] * scale;
            return true;
        }

        private void MaybeBannerImpression(Banner b)
        {
            if (!b.Loaded || !b.Visible || b.Impression) return;
            b.Impression = true;
            _queue.PostDelayed(0.3, () =>
            {
                if (_banners.ContainsKey(b.Handle) && b.Visible) Emit(b.Handle, "shown", null);
                else b.Impression = false;
            });
        }

        internal Rect BannerRect(Banner b, float screenWidth, float screenHeight)
        {
            var scale = Scale();
            var w = BannerSizes[b.SizeIndex, 0] * scale;
            var h = BannerSizes[b.SizeIndex, 1] * scale;
            float x, y;
            switch ((AdPosition.Kind)b.Position)
            {
                case AdPosition.Kind.Top: x = (screenWidth - w) / 2; y = 0; break;
                case AdPosition.Kind.Center: x = (screenWidth - w) / 2; y = (screenHeight - h) / 2; break;
                case AdPosition.Kind.TopLeft: x = 0; y = 0; break;
                case AdPosition.Kind.TopRight: x = screenWidth - w; y = 0; break;
                case AdPosition.Kind.BottomLeft: x = 0; y = screenHeight - h; break;
                case AdPosition.Kind.BottomRight: x = screenWidth - w; y = screenHeight - h; break;
                case AdPosition.Kind.Custom: x = b.X; y = b.Y; break;
                default: x = (screenWidth - w) / 2; y = screenHeight - h; break;
            }
            return new Rect(x, y, w, h);
        }

        internal void ClickBanner(int handle)
        {
            if (_banners.ContainsKey(handle)) Emit(handle, "clicked", null);
        }

        // Full screen

        public void FullScreenLoad(int handle, AdFormat format, string placementId, string customData)
        {
            var ad = new FullScreen { Handle = handle, Format = format, PlacementId = placementId };
            _fullScreens[handle] = ad;
            var error = LoadError();
            _queue.PostDelayed(AdfactenMock.LoadDelay, () =>
            {
                if (!_fullScreens.ContainsKey(handle)) return;
                if (error != null)
                {
                    _fullScreens.Remove(handle);
                    Emit(handle, "failed", error);
                    return;
                }
                ad.Loaded = true;
                string payload = null;
                if (format == AdFormat.Rewarded)
                {
                    payload = FlatJson.Write(new[]
                    {
                        new KeyValuePair<string, object>("rewardType", RewardType),
                        new KeyValuePair<string, object>("rewardAmount", RewardAmount),
                    });
                }
                Emit(handle, "loaded", payload);
            });
        }

        public void FullScreenShow(int handle)
        {
            FullScreen ad;
            if (!_fullScreens.TryGetValue(handle, out ad) || !ad.Loaded)
            {
                Emit(handle, "failedToShow", ErrorJson("not_ready", "the ad is not loaded"));
                return;
            }
            if (ad.ShowCalled)
            {
                Emit(handle, "failedToShow", ErrorJson("already_shown", "this ad was already shown; load a new one"));
                return;
            }
            if (Showing != null)
            {
                Emit(handle, "failedToShow", ErrorJson("show_failed", "another Adfacten ad is already on screen"));
                return;
            }
            ad.ShowCalled = true;
            ad.Showing = true;
            ad.ShowStartedAt = _queue.Now;
            _queue.PostDelayed(0.2, () => { if (ad.Showing) Emit(handle, "shown", null); });
            if (ad.Format == AdFormat.Rewarded)
            {
                _queue.PostDelayed(AdfactenMock.RewardedDuration, () =>
                {
                    if (!ad.Showing || ad.Rewarded) return;
                    ad.Rewarded = true;
                    Emit(handle, "reward", FlatJson.Write(new[]
                    {
                        new KeyValuePair<string, object>("type", RewardType),
                        new KeyValuePair<string, object>("amount", RewardAmount),
                    }));
                });
            }
        }

        public bool FullScreenIsReady(int handle)
        {
            FullScreen ad;
            return _fullScreens.TryGetValue(handle, out ad) && ad.Loaded && !ad.ShowCalled;
        }

        public void FullScreenDestroy(int handle)
        {
            FullScreen ad;
            if (_fullScreens.TryGetValue(handle, out ad) && ad.Showing) return; // closed by the user later
            _fullScreens.Remove(handle);
        }

        /// <summary>Seconds left before a rewarded placeholder grants the reward (0 when granted or not rewarded).</summary>
        internal double RewardSecondsLeft(FullScreen ad)
        {
            if (ad.Format != AdFormat.Rewarded || ad.Rewarded) return 0;
            return System.Math.Max(0, AdfactenMock.RewardedDuration - (_queue.Now - ad.ShowStartedAt));
        }

        internal void ClickFullScreen(int handle)
        {
            FullScreen ad;
            if (_fullScreens.TryGetValue(handle, out ad) && ad.Showing) Emit(handle, "clicked", null);
        }

        internal void CloseFullScreen(int handle)
        {
            FullScreen ad;
            if (!_fullScreens.TryGetValue(handle, out ad) || !ad.Showing) return;
            ad.Showing = false;
            _fullScreens.Remove(handle);
            Emit(handle, "closed", null);
        }

        // Native

        public void NativeLoad(int handle, string placementId)
        {
            var ad = new Native { Handle = handle, PlacementId = placementId };
            _natives[handle] = ad;
            var error = LoadError();
            _queue.PostDelayed(AdfactenMock.LoadDelay, () =>
            {
                if (!_natives.ContainsKey(handle)) return;
                if (error != null)
                {
                    _natives.Remove(handle);
                    Emit(handle, "failed", error);
                    return;
                }
                ad.Loaded = true;
                Emit(handle, "loaded", FlatJson.Write(new[]
                {
                    new KeyValuePair<string, object>("title", "Test native ad"),
                    new KeyValuePair<string, object>("body", "Editor placeholder. Real ads appear on Android and iOS devices."),
                    new KeyValuePair<string, object>("brand", "Adfacten"),
                    new KeyValuePair<string, object>("callToAction", "Open"),
                    new KeyValuePair<string, object>("adLabel", "Ad"),
                    new KeyValuePair<string, object>("hasImage", true),
                    new KeyValuePair<string, object>("hasIcon", true),
                    new KeyValuePair<string, object>("imageWidth", 1200),
                    new KeyValuePair<string, object>("imageHeight", 628),
                }));
                MaybeNativeImpression(ad);
            });
        }

        public void NativeShow(int handle, int template, float x, float y, float width, float height, string styleJson)
        {
            Native ad;
            if (!_natives.TryGetValue(handle, out ad)) return;
            ad.Template = template;
            ad.Rect = new Rect(x, y, width, height);
            ad.Visible = true;
            MaybeNativeImpression(ad);
        }

        public void NativeHide(int handle)
        {
            Native ad;
            if (_natives.TryGetValue(handle, out ad)) ad.Visible = false;
        }

        public void NativeDestroy(int handle)
        {
            _natives.Remove(handle);
        }

        private void MaybeNativeImpression(Native ad)
        {
            if (!ad.Loaded || !ad.Visible || ad.Impression) return;
            ad.Impression = true;
            _queue.PostDelayed(0.2, () => { if (_natives.ContainsKey(ad.Handle)) Emit(ad.Handle, "shown", null); });
        }

        internal void ClickNative(int handle)
        {
            Native ad;
            if (_natives.TryGetValue(handle, out ad) && ad.Visible) Emit(handle, "clicked", null);
        }

        // Tracking

        public int TrackingAuthorizationStatus { get { return -1; } }

        public void RequestTrackingAuthorization(int handle)
        {
            Emit(handle, "tracking", "{\"status\":-1}");
        }

        // Helpers

        private string LoadError()
        {
            if (!_initialized) return ErrorJson("not_initialized", "Adfacten.Initialize was not called");
            var code = AdfactenMock.LoadError;
            if (code.HasValue) return ErrorJson(AdError.NameOf(code.Value), "simulated by AdfactenMock.LoadError");
            return null;
        }

        private static string ErrorJson(string name, string message)
        {
            return FlatJson.Write(new[]
            {
                new KeyValuePair<string, object>("name", name),
                new KeyValuePair<string, object>("message", message),
            });
        }

        /// <summary>Events are always delivered later, like the real bridges, never inside the caller.</summary>
        private void Emit(int handle, string name, string payload)
        {
            _queue.Post(() => AdfactenRuntime.Router.Dispatch(handle, name, payload));
        }

        private float Scale()
        {
            if (PixelsPerDp > 0) return PixelsPerDp;
            var dpi = Screen.dpi;
            return dpi > 0 ? Mathf.Max(1f, dpi / 160f) : 1f;
        }
    }
}

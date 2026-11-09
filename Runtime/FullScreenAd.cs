using System;
using System.Collections.Generic;
using AdfactenAds.Internal;

namespace AdfactenAds
{
    /// <summary>
    /// Base class of <see cref="InterstitialAd"/>, <see cref="RewardedAd"/> and <see cref="AppOpenAd"/>.
    /// A loaded ad can be shown once and is valid for about 55 minutes. It is released after
    /// <see cref="OnClosed"/>; call <see cref="Destroy"/> if you drop an ad without showing it.
    /// </summary>
    public abstract class FullScreenAd : IAdEventTarget
    {
        private readonly AdFormat _format;
        private bool _loaded;
        private bool _showCalled;
        private Action<FullScreenAd, AdError> _loadCallback;

        internal FullScreenAd(AdFormat format, string placementId)
        {
            _format = format;
            PlacementId = placementId;
            Handle = AdfactenRuntime.NewHandle();
        }

        /// <summary>The ad is on screen (the impression was recorded).</summary>
        public event Action OnShown;

        public event Action OnClicked;

        /// <summary>The ad was dismissed. Resume the game here and load the next ad.</summary>
        public event Action OnClosed;

        /// <summary>
        /// <see cref="Show"/> could not present the ad. Unless the code is <see cref="AdErrorCode.ShowFailed"/>
        /// (another ad on screen, app in the background), the ad cannot be used any more: load a new one.
        /// </summary>
        public event Action<AdError> OnFailedToShow;

        /// <summary>The placement (<c>plc_…</c>) this ad was loaded for.</summary>
        public string PlacementId { get; private set; }

        public bool IsDestroyed { get; private set; }

        internal int Handle { get; private set; }

        /// <summary>Whether the ad can be shown now (loaded, not shown yet, not expired).</summary>
        public bool IsReady
        {
            get { return _loaded && !IsDestroyed && !_showCalled && AdfactenRuntime.Client.FullScreenIsReady(Handle); }
        }

        /// <summary>Releases the ad. Safe to call twice. An ad on screen stays until closed, without events.</summary>
        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            _loadCallback = null;
            AdfactenRuntime.Router.Unregister(Handle);
            AdfactenRuntime.Client.FullScreenDestroy(Handle);
        }

        internal void ShowInternal()
        {
            AdfactenRuntime.EnsureStarted();
            AdError error = null;
            if (IsDestroyed) error = new AdError(AdErrorCode.NotReady, "the ad was destroyed; load a new one");
            else if (_showCalled) error = new AdError(AdErrorCode.AlreadyShown, "this ad was already shown; load a new one");
            if (error != null)
            {
                AdfactenRuntime.Queue.Post(() => RaiseFailedToShow(error));
                return;
            }
            _showCalled = true;
            AdfactenRuntime.Client.FullScreenShow(Handle);
        }

        internal static void Load<T>(T ad, string customData, Action<T, AdError> callback) where T : FullScreenAd
        {
            AdfactenRuntime.EnsureStarted();
            if (string.IsNullOrEmpty(ad.PlacementId))
            {
                var error = new AdError(AdErrorCode.InvalidRequest, "placementId is empty");
                ad.IsDestroyed = true;
                AdfactenRuntime.Queue.Post(() => { if (callback != null) callback(null, error); });
                return;
            }
            ad._loadCallback = (a, e) => { if (callback != null) callback((T)a, e); };
            AdfactenRuntime.Router.Register(ad.Handle, ad);
            AdfactenRuntime.Client.FullScreenLoad(ad.Handle, ad._format, ad.PlacementId, customData);
        }

        internal static string Normalize(string placementId)
        {
            return placementId == null ? string.Empty : placementId.Trim();
        }

        /// <summary>Format-specific data from the <c>loaded</c> event.</summary>
        internal virtual void OnLoadedPayload(Dictionary<string, object> payload)
        {
        }

        internal virtual void OnReward(AdReward reward)
        {
        }

        private void RaiseFailedToShow(AdError error)
        {
            var h = OnFailedToShow;
            if (h != null) h(error);
        }

        void IAdEventTarget.HandleEvent(string name, Dictionary<string, object> payload)
        {
            if (IsDestroyed) return;
            Action h = null;
            switch (name)
            {
                case "loaded":
                {
                    _loaded = true;
                    OnLoadedPayload(payload);
                    var cb = _loadCallback;
                    _loadCallback = null;
                    if (cb != null) cb(this, null);
                    return;
                }
                case "failed":
                {
                    var cb = _loadCallback;
                    _loadCallback = null;
                    IsDestroyed = true;
                    AdfactenRuntime.Router.Unregister(Handle);
                    AdfactenRuntime.Client.FullScreenDestroy(Handle);
                    if (cb != null) cb(null, AdError.FromPayload(payload));
                    return;
                }
                case "shown": h = OnShown; break;
                case "clicked": h = OnClicked; break;
                case "reward":
                    OnReward(new AdReward(FlatJson.GetString(payload, "type"), FlatJson.GetDouble(payload, "amount")));
                    return;
                case "failedToShow":
                {
                    var error = AdError.FromPayload(payload);
                    // Only "another ad is on screen" leaves the ad usable.
                    if (error.Code == AdErrorCode.ShowFailed) _showCalled = false;
                    RaiseFailedToShow(error);
                    return;
                }
                case "closed":
                    h = OnClosed;
                    Destroy();
                    break;
            }
            if (h != null) h();
        }
    }

    /// <summary>A full-screen ad shown at natural breaks (between levels, screens…).</summary>
    public sealed class InterstitialAd : FullScreenAd
    {
        private InterstitialAd(string placementId) : base(AdFormat.Interstitial, placementId) { }

        /// <summary>Loads an interstitial. <paramref name="callback"/> gets the ad, or null and the error.</summary>
        public static void Load(string placementId, Action<InterstitialAd, AdError> callback)
        {
            Load(new InterstitialAd(Normalize(placementId)), null, callback);
        }

        /// <summary>Shows the ad full screen. The game is paused while the ad is on screen.</summary>
        public void Show()
        {
            ShowInternal();
        }
    }

    /// <summary>
    /// A full-screen ad the user opts into in exchange for a reward.
    /// <para>The reward is granted server-side: Adfacten calls your reward callback URL (signed with your reward
    /// secret) after a valid completion. <see cref="OnUserEarnedReward"/> is for UI only; never grant value from it.</para>
    /// </summary>
    public sealed class RewardedAd : FullScreenAd
    {
        private Action<AdReward> _showCallback;

        private RewardedAd(string placementId) : base(AdFormat.Rewarded, placementId) { }

        /// <summary>The user finished the ad. UI only (for example "+10 coins"); credit the user from your server.</summary>
        public event Action<AdReward> OnUserEarnedReward;

        /// <summary>The reward configured on the placement, or null if the server sent none.</summary>
        public AdReward Reward { get; private set; }

        /// <summary>Loads a rewarded ad.</summary>
        /// <param name="customData">Up to 256 characters, echoed as <c>custom</c> in the server-side reward callback.</param>
        public static void Load(string placementId, string customData, Action<RewardedAd, AdError> callback)
        {
            if (customData != null && customData.Length > 256)
            {
                AdLog.Warn("RewardedAd customData is longer than 256 characters; it is cut.");
                customData = customData.Substring(0, 256);
            }
            Load(new RewardedAd(Normalize(placementId)), customData, callback);
        }

        public static void Load(string placementId, Action<RewardedAd, AdError> callback)
        {
            Load(placementId, null, callback);
        }

        /// <summary>Shows the ad. <paramref name="onUserEarnedReward"/> runs once, when the user completed it.</summary>
        public void Show(Action<AdReward> onUserEarnedReward = null)
        {
            _showCallback = onUserEarnedReward;
            ShowInternal();
        }

        internal override void OnLoadedPayload(Dictionary<string, object> payload)
        {
            var type = FlatJson.GetString(payload, "rewardType");
            Reward = type == null ? null : new AdReward(type, FlatJson.GetDouble(payload, "rewardAmount"));
        }

        internal override void OnReward(AdReward reward)
        {
            var cb = _showCallback;
            _showCallback = null;
            if (cb != null) cb(reward);
            var h = OnUserEarnedReward;
            if (h != null) h(reward);
        }
    }

    /// <summary>
    /// A full-screen ad for cold starts and returns to the game. Load it ahead of time and show it when the app
    /// comes to the foreground (<c>OnApplicationPause(false)</c>); don't keep the player waiting for it.
    /// </summary>
    public sealed class AppOpenAd : FullScreenAd
    {
        private AppOpenAd(string placementId) : base(AdFormat.AppOpen, placementId) { }

        public static void Load(string placementId, Action<AppOpenAd, AdError> callback)
        {
            Load(new AppOpenAd(Normalize(placementId)), null, callback);
        }

        public void Show()
        {
            ShowInternal();
        }
    }
}

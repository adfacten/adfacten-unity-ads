using System;
using System.Collections.Generic;
using AdfactenAds.Internal;

namespace AdfactenAds
{
    /// <summary>
    /// A banner drawn by the native SDK on top of the Unity view.
    /// <code>
    /// var banner = new BannerView("plc_…", AdSize.Banner, AdPosition.Bottom);
    /// banner.OnFailedToLoad += e => Debug.Log(e.Name);
    /// banner.Load();
    /// </code>
    /// The banner is visible after <see cref="Load"/> until you call <see cref="Hide"/>. It refreshes itself on the
    /// placement's interval (30–120 s of visible time). After a failed load it tries again on the same timer while
    /// it is visible, so keep it in place instead of destroying it on <see cref="OnFailedToLoad"/>; a refusal
    /// (<see cref="AdErrorCode.Rejected"/>, <see cref="AdErrorCode.InvalidRequest"/>) stops it until the next
    /// <see cref="Load"/>. The impression counts only when the banner is actually on screen.
    /// Call <see cref="Destroy"/> when you no longer need it.
    /// </summary>
    public sealed class BannerView : IAdEventTarget
    {
        private readonly int _handle;
        private bool _created;
        private bool _visible = true;

        public BannerView(string placementId, AdSize size, AdPosition position)
        {
            PlacementId = placementId == null ? string.Empty : placementId.Trim();
            Size = size ?? AdSize.Banner;
            Position = position ?? AdPosition.Bottom;
            _handle = AdfactenRuntime.NewHandle();
        }

        /// <summary>A creative was loaded (also after each refresh).</summary>
        public event Action OnLoaded;

        /// <summary>A load failed. A banner that already shows a creative keeps it.</summary>
        public event Action<AdError> OnFailedToLoad;

        /// <summary>The creative is on screen (the impression was recorded).</summary>
        public event Action OnShown;

        public event Action OnClicked;

        public string PlacementId { get; private set; }
        public AdSize Size { get; private set; }
        public AdPosition Position { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool IsVisible { get { return _visible; } }

        /// <summary>Requests an ad. Calling it again replaces the creative and restarts a stopped banner.</summary>
        public void Load()
        {
            if (IsDestroyed)
            {
                AdLog.Warn("BannerView.Load called after Destroy.");
                return;
            }
            AdfactenRuntime.EnsureStarted();
            if (PlacementId.Length == 0)
            {
                var error = new AdError(AdErrorCode.InvalidRequest, "placementId is empty");
                AdfactenRuntime.Queue.Post(() => Fail(error));
                return;
            }
            EnsureCreated();
            AdfactenRuntime.Client.BannerLoad(_handle);
        }

        /// <summary>Shows a hidden banner. Refresh and retries resume.</summary>
        public void Show()
        {
            SetVisible(true);
        }

        /// <summary>Hides the banner. It does not refresh or count impressions while hidden.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        public void SetPosition(AdPosition position)
        {
            if (IsDestroyed || position == null) return;
            Position = position;
            if (_created) AdfactenRuntime.Client.BannerSetPosition(_handle, (int)position.Type, position.X, position.Y);
        }

        /// <summary>Banner width in Unity screen pixels (0 before the first <see cref="Load"/>).</summary>
        public float GetWidthInPixels()
        {
            float w, h;
            return _created && !IsDestroyed && AdfactenRuntime.Client.BannerSizeInPixels(_handle, out w, out h) ? w : 0;
        }

        /// <summary>Banner height in Unity screen pixels (0 before the first <see cref="Load"/>).</summary>
        public float GetHeightInPixels()
        {
            float w, h;
            return _created && !IsDestroyed && AdfactenRuntime.Client.BannerSizeInPixels(_handle, out w, out h) ? h : 0;
        }

        /// <summary>Removes the banner and releases it. Safe to call twice.</summary>
        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            AdfactenRuntime.Router.Unregister(_handle);
            if (_created) AdfactenRuntime.Client.BannerDestroy(_handle);
            _created = false;
        }

        private void SetVisible(bool visible)
        {
            if (IsDestroyed) return;
            _visible = visible;
            if (_created) AdfactenRuntime.Client.BannerSetVisible(_handle, visible);
        }

        private void EnsureCreated()
        {
            if (_created) return;
            _created = true;
            AdfactenRuntime.Router.Register(_handle, this);
            var client = AdfactenRuntime.Client;
            client.BannerCreate(_handle, PlacementId, Size.Index);
            client.BannerSetPosition(_handle, (int)Position.Type, Position.X, Position.Y);
            client.BannerSetVisible(_handle, _visible);
        }

        private void Fail(AdError error)
        {
            if (IsDestroyed) return;
            var h = OnFailedToLoad;
            if (h != null) h(error);
        }

        void IAdEventTarget.HandleEvent(string name, Dictionary<string, object> payload)
        {
            if (IsDestroyed) return;
            Action h = null;
            switch (name)
            {
                case "loaded": h = OnLoaded; break;
                case "shown": h = OnShown; break;
                case "clicked": h = OnClicked; break;
                case "failed": Fail(AdError.FromPayload(payload)); return;
            }
            if (h != null) h();
        }
    }
}

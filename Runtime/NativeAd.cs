using System;
using System.Collections.Generic;
using AdfactenAds.Internal;
using UnityEngine;

namespace AdfactenAds
{
    /// <summary>Layouts for <see cref="NativeAd.Show"/>.</summary>
    public enum NativeAdTemplate
    {
        /// <summary>A row: icon (or a square crop of the image), ad label, brand, title, one line of body and a compact button. About 80 dp / pt high.</summary>
        Small = 0,

        /// <summary>A card: icon, title and brand, the main image, body and a full-width button. Give it at least 300 dp / pt of height.</summary>
        Medium = 1,
    }

    /// <summary>Colors and shape of a native template. Unset values use the template's light theme.</summary>
    public sealed class NativeAdStyle
    {
        public Color? BackgroundColor { get; set; }
        public Color? TitleColor { get; set; }
        public Color? BodyColor { get; set; }
        public Color? BrandColor { get; set; }
        public Color? CallToActionBackgroundColor { get; set; }
        public Color? CallToActionTextColor { get; set; }
        public Color? AdLabelBackgroundColor { get; set; }
        public Color? AdLabelTextColor { get; set; }

        /// <summary>Corner radius in dp / pt (default 12).</summary>
        public float? CornerRadius { get; set; }

        /// <summary>Button corner radius in dp / pt (default 20).</summary>
        public float? CallToActionCornerRadius { get; set; }

        internal string ToJson()
        {
            var map = new List<KeyValuePair<string, object>>();
            AddColor(map, "backgroundColor", BackgroundColor);
            AddColor(map, "titleColor", TitleColor);
            AddColor(map, "bodyColor", BodyColor);
            AddColor(map, "brandColor", BrandColor);
            AddColor(map, "callToActionBackgroundColor", CallToActionBackgroundColor);
            AddColor(map, "callToActionTextColor", CallToActionTextColor);
            AddColor(map, "adLabelBackgroundColor", AdLabelBackgroundColor);
            AddColor(map, "adLabelTextColor", AdLabelTextColor);
            if (CornerRadius.HasValue) map.Add(new KeyValuePair<string, object>("cornerRadius", Math.Max(0f, CornerRadius.Value)));
            if (CallToActionCornerRadius.HasValue) map.Add(new KeyValuePair<string, object>("callToActionCornerRadius", Math.Max(0f, CallToActionCornerRadius.Value)));
            return FlatJson.Write(map);
        }

        /// <summary>0xAARRGGBB, as an unsigned number.</summary>
        internal static long ToArgb(Color c)
        {
            Color32 c32 = c;
            return ((long)c32.a << 24) | ((long)c32.r << 16) | ((long)c32.g << 8) | c32.b;
        }

        private static void AddColor(List<KeyValuePair<string, object>> map, string key, Color? color)
        {
            if (color.HasValue) map.Add(new KeyValuePair<string, object>(key, ToArgb(color.Value)));
        }
    }

    /// <summary>
    /// A native ad, shown in a native template placed over the Unity view at a screen rectangle.
    /// <para>Unity UI cannot host Android or iOS views, so the template is drawn by the native SDK on top of the
    /// game. That is what records impressions and clicks: an ad drawn with Unity UI from the asset properties is
    /// not tracked and earns nothing. Keep the rectangle clear of other UI, and move it (call
    /// <see cref="Show"/> again) when your layout changes.</para>
    /// </summary>
    public sealed class NativeAd : IAdEventTarget
    {
        private readonly int _handle;
        private Action<NativeAd, AdError> _loadCallback;

        private NativeAd(string placementId)
        {
            PlacementId = placementId;
            _handle = AdfactenRuntime.NewHandle();
        }

        /// <summary>The ad is on screen (the impression was recorded).</summary>
        public event Action OnShown;

        public event Action OnClicked;

        public string PlacementId { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }

        /// <summary>The advertiser or brand name.</summary>
        public string Brand { get; private set; }

        public string CallToAction { get; private set; }

        /// <summary>The disclosure text the template shows, e.g. "Ad" or "Reklam".</summary>
        public string AdLabel { get; private set; }

        /// <summary>Whether the main image / icon was downloaded and will be shown.</summary>
        public bool HasImage { get; private set; }

        public bool HasIcon { get; private set; }

        /// <summary>Main image size in pixels as sent by the server, or 0 when unknown.</summary>
        public float ImageWidth { get; private set; }

        public float ImageHeight { get; private set; }

        public bool IsVisible { get; private set; }
        public bool IsDestroyed { get; private set; }

        /// <summary>Loads a native ad (its images are downloaded first).</summary>
        public static void Load(string placementId, Action<NativeAd, AdError> callback)
        {
            AdfactenRuntime.EnsureStarted();
            var ad = new NativeAd(placementId == null ? string.Empty : placementId.Trim());
            if (ad.PlacementId.Length == 0)
            {
                var error = new AdError(AdErrorCode.InvalidRequest, "placementId is empty");
                ad.IsDestroyed = true;
                AdfactenRuntime.Queue.Post(() => { if (callback != null) callback(null, error); });
                return;
            }
            ad._loadCallback = callback;
            AdfactenRuntime.Router.Register(ad._handle, ad);
            AdfactenRuntime.Client.NativeLoad(ad._handle, ad.PlacementId);
        }

        /// <summary>
        /// Shows the ad in <paramref name="template"/> at <paramref name="screenRect"/>: Unity screen pixels, measured
        /// from the top-left corner (see <see cref="AdfactenScreen.FromRectTransform"/>). Calling it again moves the
        /// template. An ad is shown in one place at a time and records one impression.
        /// </summary>
        public void Show(NativeAdTemplate template, Rect screenRect, NativeAdStyle style = null)
        {
            if (IsDestroyed)
            {
                AdLog.Warn("NativeAd.Show called after Destroy.");
                return;
            }
            if (screenRect.width <= 0 || screenRect.height <= 0)
            {
                AdLog.Warn("NativeAd.Show: the rectangle is empty.");
                return;
            }
            IsVisible = true;
            AdfactenRuntime.Client.NativeShow(_handle, (int)template, screenRect.x, screenRect.y, screenRect.width, screenRect.height,
                style == null ? "{}" : style.ToJson());
        }

        /// <summary>Removes the template from the screen. <see cref="Show"/> brings it back.</summary>
        public void Hide()
        {
            if (IsDestroyed || !IsVisible) return;
            IsVisible = false;
            AdfactenRuntime.Client.NativeHide(_handle);
        }

        /// <summary>Removes the template and releases the ad. Safe to call twice.</summary>
        public void Destroy()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            IsVisible = false;
            _loadCallback = null;
            AdfactenRuntime.Router.Unregister(_handle);
            AdfactenRuntime.Client.NativeDestroy(_handle);
        }

        void IAdEventTarget.HandleEvent(string name, Dictionary<string, object> payload)
        {
            if (IsDestroyed) return;
            switch (name)
            {
                case "loaded":
                {
                    Title = Text(payload, "title");
                    Body = Text(payload, "body");
                    Brand = Text(payload, "brand");
                    CallToAction = Text(payload, "callToAction");
                    AdLabel = Text(payload, "adLabel") ?? "Ad";
                    HasImage = FlatJson.GetBool(payload, "hasImage");
                    HasIcon = FlatJson.GetBool(payload, "hasIcon");
                    ImageWidth = (float)FlatJson.GetDouble(payload, "imageWidth");
                    ImageHeight = (float)FlatJson.GetDouble(payload, "imageHeight");
                    var cb = _loadCallback;
                    _loadCallback = null;
                    if (cb != null) cb(this, null);
                    return;
                }
                case "failed":
                {
                    var cb = _loadCallback;
                    Destroy();
                    if (cb != null) cb(null, AdError.FromPayload(payload));
                    return;
                }
                case "shown":
                {
                    var h = OnShown;
                    if (h != null) h();
                    return;
                }
                case "clicked":
                {
                    var h = OnClicked;
                    if (h != null) h();
                    return;
                }
            }
        }

        private static string Text(Dictionary<string, object> payload, string key)
        {
            var s = FlatJson.GetString(payload, key);
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }
    }
}

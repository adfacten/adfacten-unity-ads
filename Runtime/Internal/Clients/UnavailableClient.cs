using System.Collections.Generic;

namespace AdfactenAds.Internal
{
    /// <summary>Release builds on platforms without a native SDK: nothing is shown and every load fails.</summary>
    internal sealed class UnavailableClient : IAdfactenClient
    {
        private const string Message = "Adfacten Ads is available on Android and iOS only";
        private bool _warned;

        public string SdkVersion { get { return "unavailable"; } }
        public bool IsInitialized { get { return false; } }

        public void SetWrapper(string name, string version) { }

        public void Initialize(string appKey, AdfactenConfig.Resolved config)
        {
            Warn();
        }

        public void SetUserId(string userId) { }

        public void BannerCreate(int handle, string placementId, int sizeIndex) { }
        public void BannerSetPosition(int handle, int position, float x, float y) { }
        public void BannerLoad(int handle) { Fail(handle, "failed"); }
        public void BannerSetVisible(int handle, bool visible) { }
        public void BannerDestroy(int handle) { }

        public bool BannerSizeInPixels(int handle, out float width, out float height)
        {
            width = height = 0;
            return false;
        }

        public void FullScreenLoad(int handle, AdFormat format, string placementId, string customData) { Fail(handle, "failed"); }
        public void FullScreenShow(int handle) { Fail(handle, "failedToShow"); }
        public bool FullScreenIsReady(int handle) { return false; }
        public void FullScreenDestroy(int handle) { }

        public void NativeLoad(int handle, string placementId) { Fail(handle, "failed"); }
        public void NativeShow(int handle, int template, float x, float y, float width, float height, string styleJson) { }
        public void NativeHide(int handle) { }
        public void NativeDestroy(int handle) { }

        public int TrackingAuthorizationStatus { get { return -1; } }

        public void RequestTrackingAuthorization(int handle)
        {
            AdfactenRuntime.PostEvent(handle, "tracking", "{\"status\":-1}");
        }

        private void Fail(int handle, string evt)
        {
            Warn();
            AdfactenRuntime.PostEvent(handle, evt, FlatJson.Write(new[]
            {
                new KeyValuePair<string, object>("name", "internal_error"),
                new KeyValuePair<string, object>("message", Message),
            }));
        }

        private void Warn()
        {
            if (_warned) return;
            _warned = true;
            AdLog.Warn(Message + "; ads are disabled on this platform.");
        }
    }
}

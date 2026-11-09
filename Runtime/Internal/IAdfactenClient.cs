using System.Collections.Generic;

namespace AdfactenAds.Internal
{
    internal enum AdFormat
    {
        Interstitial = 0,
        Rewarded = 1,
        AppOpen = 2,
    }

    /// <summary>An object that receives bridge events for its handle, on the main thread.</summary>
    internal interface IAdEventTarget
    {
        void HandleEvent(string name, Dictionary<string, object> payload);
    }

    /// <summary>
    /// One platform implementation (Android, iOS, Editor mock). Coordinates are Unity screen pixels from the
    /// top-left corner. Events come back through <see cref="AdfactenRuntime.PostEvent"/> from any thread:
    /// <c>loaded</c>, <c>failed</c>, <c>shown</c>, <c>clicked</c>, <c>closed</c>, <c>failedToShow</c>,
    /// <c>reward</c>, <c>tracking</c>.
    /// </summary>
    internal interface IAdfactenClient
    {
        string SdkVersion { get; }
        bool IsInitialized { get; }

        void SetWrapper(string name, string version);
        void Initialize(string appKey, AdfactenConfig.Resolved config);
        void SetUserId(string userId);

        void BannerCreate(int handle, string placementId, int sizeIndex);
        void BannerSetPosition(int handle, int position, float x, float y);
        void BannerLoad(int handle);
        void BannerSetVisible(int handle, bool visible);
        void BannerDestroy(int handle);
        bool BannerSizeInPixels(int handle, out float width, out float height);

        void FullScreenLoad(int handle, AdFormat format, string placementId, string customData);
        void FullScreenShow(int handle);
        bool FullScreenIsReady(int handle);
        void FullScreenDestroy(int handle);

        void NativeLoad(int handle, string placementId);
        void NativeShow(int handle, int template, float x, float y, float width, float height, string styleJson);
        void NativeHide(int handle);
        void NativeDestroy(int handle);

        int TrackingAuthorizationStatus { get; }
        void RequestTrackingAuthorization(int handle);
    }
}

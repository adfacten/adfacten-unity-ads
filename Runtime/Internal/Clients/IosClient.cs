#if UNITY_IOS
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AdfactenAds.Internal
{
    /// <summary>
    /// Talks to the <c>AdfactenUnity_*</c> functions in Plugins/iOS/AdfactenUnityBridge.mm. The bridge runs on the
    /// iOS main thread; its events are queued for the Unity main thread like on Android.
    /// </summary>
    internal sealed class IosClient : IAdfactenClient
    {
        private delegate void EventCallback(int handle, IntPtr name, IntPtr payload);

        private static readonly EventCallback Callback = OnEvent;

        public IosClient()
        {
            AdfactenUnity_SetEventCallback(Callback);
        }

        [AOT.MonoPInvokeCallback(typeof(EventCallback))]
        private static void OnEvent(int handle, IntPtr name, IntPtr payload)
        {
            AdfactenRuntime.PostEvent(handle, Utf8(name), Utf8(payload));
        }

        private static string Utf8(IntPtr p)
        {
            return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
        }

        public string SdkVersion { get { return AdfactenUnity_SdkVersion(); } }
        public bool IsInitialized { get { return AdfactenUnity_IsInitialized() != 0; } }

        public void SetWrapper(string name, string version)
        {
            AdfactenUnity_SetWrapper(name, version);
        }

        public void Initialize(string appKey, AdfactenConfig.Resolved config)
        {
            AdfactenUnity_Initialize(appKey, config.TestMode ? 1 : 0, (int)config.LogLevel, config.ChildDirected ? 1 : 0, config.UserId);
        }

        public void SetUserId(string userId)
        {
            AdfactenUnity_SetUserId(userId);
        }

        public void BannerCreate(int handle, string placementId, int sizeIndex)
        {
            AdfactenUnity_BannerCreate(handle, placementId, sizeIndex);
        }

        public void BannerSetPosition(int handle, int position, float x, float y)
        {
            AdfactenUnity_BannerSetPosition(handle, position, x, y, Screen.width, Screen.height);
        }

        public void BannerLoad(int handle)
        {
            AdfactenUnity_BannerLoad(handle);
        }

        public void BannerSetVisible(int handle, bool visible)
        {
            AdfactenUnity_BannerSetVisible(handle, visible ? 1 : 0);
        }

        public void BannerDestroy(int handle)
        {
            AdfactenUnity_BannerDestroy(handle);
        }

        public bool BannerSizeInPixels(int handle, out float width, out float height)
        {
            return AdfactenUnity_BannerSize(handle, Screen.width, Screen.height, out width, out height) != 0;
        }

        public void FullScreenLoad(int handle, AdFormat format, string placementId, string customData)
        {
            AdfactenUnity_FullScreenLoad(handle, (int)format, placementId, customData);
        }

        public void FullScreenShow(int handle)
        {
            AdfactenUnity_FullScreenShow(handle);
        }

        public bool FullScreenIsReady(int handle)
        {
            return AdfactenUnity_FullScreenIsReady(handle) != 0;
        }

        public void FullScreenDestroy(int handle)
        {
            AdfactenUnity_FullScreenDestroy(handle);
        }

        public void NativeLoad(int handle, string placementId)
        {
            AdfactenUnity_NativeLoad(handle, placementId);
        }

        public void NativeShow(int handle, int template, float x, float y, float width, float height, string styleJson)
        {
            AdfactenUnity_NativeShow(handle, template, x, y, width, height, Screen.width, Screen.height, styleJson);
        }

        public void NativeHide(int handle)
        {
            AdfactenUnity_NativeHide(handle);
        }

        public void NativeDestroy(int handle)
        {
            AdfactenUnity_NativeDestroy(handle);
        }

        public int TrackingAuthorizationStatus { get { return AdfactenUnity_TrackingStatus(); } }

        public void RequestTrackingAuthorization(int handle)
        {
            AdfactenUnity_RequestTracking(handle);
        }

        [DllImport("__Internal")] private static extern void AdfactenUnity_SetEventCallback(EventCallback callback);
        [DllImport("__Internal")] private static extern string AdfactenUnity_SdkVersion();
        [DllImport("__Internal")] private static extern int AdfactenUnity_IsInitialized();
        [DllImport("__Internal")] private static extern void AdfactenUnity_SetWrapper(string name, string version);
        [DllImport("__Internal")] private static extern void AdfactenUnity_Initialize(string appKey, int testMode, int logLevel, int childDirected, string userId);
        [DllImport("__Internal")] private static extern void AdfactenUnity_SetUserId(string userId);

        [DllImport("__Internal")] private static extern void AdfactenUnity_BannerCreate(int handle, string placementId, int sizeIndex);
        [DllImport("__Internal")] private static extern void AdfactenUnity_BannerSetPosition(int handle, int position, float x, float y, float screenWidth, float screenHeight);
        [DllImport("__Internal")] private static extern void AdfactenUnity_BannerLoad(int handle);
        [DllImport("__Internal")] private static extern void AdfactenUnity_BannerSetVisible(int handle, int visible);
        [DllImport("__Internal")] private static extern void AdfactenUnity_BannerDestroy(int handle);
        [DllImport("__Internal")] private static extern int AdfactenUnity_BannerSize(int handle, float screenWidth, float screenHeight, out float width, out float height);

        [DllImport("__Internal")] private static extern void AdfactenUnity_FullScreenLoad(int handle, int format, string placementId, string customData);
        [DllImport("__Internal")] private static extern void AdfactenUnity_FullScreenShow(int handle);
        [DllImport("__Internal")] private static extern int AdfactenUnity_FullScreenIsReady(int handle);
        [DllImport("__Internal")] private static extern void AdfactenUnity_FullScreenDestroy(int handle);

        [DllImport("__Internal")] private static extern void AdfactenUnity_NativeLoad(int handle, string placementId);
        [DllImport("__Internal")] private static extern void AdfactenUnity_NativeShow(int handle, int template, float x, float y, float width, float height, float screenWidth, float screenHeight, string styleJson);
        [DllImport("__Internal")] private static extern void AdfactenUnity_NativeHide(int handle);
        [DllImport("__Internal")] private static extern void AdfactenUnity_NativeDestroy(int handle);

        [DllImport("__Internal")] private static extern int AdfactenUnity_TrackingStatus();
        [DllImport("__Internal")] private static extern void AdfactenUnity_RequestTracking(int handle);
    }
}
#endif

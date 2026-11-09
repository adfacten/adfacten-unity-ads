#if UNITY_ANDROID
using UnityEngine;
using UnityEngine.Scripting;

namespace AdfactenAds.Internal
{
    /// <summary>
    /// Talks to <c>com.adfacten.unity.AdfactenUnityBridge</c> (Plugins/Android), which drives the Android SDK on the
    /// UI thread. Events arrive on the Android UI thread and are queued for the Unity main thread.
    /// Strings are never passed as null (JNI signatures would not match); the bridge reads "" as "not set".
    /// </summary>
    internal sealed class AndroidClient : IAdfactenClient
    {
        private const string BridgeClass = "com.adfacten.unity.AdfactenUnityBridge";
        private const string ListenerInterface = "com.adfacten.unity.AdfactenUnityListener";

        private readonly AndroidJavaClass _bridge;
        private AndroidJavaObject _activity;

        public AndroidClient()
        {
            _bridge = new AndroidJavaClass(BridgeClass);
            _bridge.CallStatic("setListener", new Listener());
        }

        private AndroidJavaObject Activity
        {
            get
            {
                if (_activity == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    }
                }
                return _activity;
            }
        }

        public string SdkVersion { get { return _bridge.CallStatic<string>("sdkVersion"); } }
        public bool IsInitialized { get { return _bridge.CallStatic<bool>("isInitialized"); } }

        public void SetWrapper(string name, string version)
        {
            _bridge.CallStatic("setWrapper", name, version);
        }

        public void Initialize(string appKey, AdfactenConfig.Resolved config)
        {
            _bridge.CallStatic("initialize", Activity, appKey, config.TestMode, (int)config.LogLevel, config.ChildDirected, config.UserId ?? "");
        }

        public void SetUserId(string userId)
        {
            _bridge.CallStatic("setUserId", userId ?? "");
        }

        public void BannerCreate(int handle, string placementId, int sizeIndex)
        {
            _bridge.CallStatic("bannerCreate", Activity, handle, placementId, sizeIndex);
        }

        public void BannerSetPosition(int handle, int position, float x, float y)
        {
            _bridge.CallStatic("bannerSetPosition", handle, position, x, y, Screen.width, Screen.height);
        }

        public void BannerLoad(int handle)
        {
            _bridge.CallStatic("bannerLoad", handle);
        }

        public void BannerSetVisible(int handle, bool visible)
        {
            _bridge.CallStatic("bannerSetVisible", handle, visible);
        }

        public void BannerDestroy(int handle)
        {
            _bridge.CallStatic("bannerDestroy", handle);
        }

        public bool BannerSizeInPixels(int handle, out float width, out float height)
        {
            var size = _bridge.CallStatic<float[]>("bannerSize", handle, Screen.width, Screen.height);
            if (size == null || size.Length < 2)
            {
                width = height = 0;
                return false;
            }
            width = size[0];
            height = size[1];
            return true;
        }

        public void FullScreenLoad(int handle, AdFormat format, string placementId, string customData)
        {
            _bridge.CallStatic("fullScreenLoad", Activity, handle, (int)format, placementId, customData ?? "");
        }

        public void FullScreenShow(int handle)
        {
            _bridge.CallStatic("fullScreenShow", Activity, handle);
        }

        public bool FullScreenIsReady(int handle)
        {
            return _bridge.CallStatic<bool>("fullScreenIsReady", handle);
        }

        public void FullScreenDestroy(int handle)
        {
            _bridge.CallStatic("fullScreenDestroy", handle);
        }

        public void NativeLoad(int handle, string placementId)
        {
            _bridge.CallStatic("nativeLoad", Activity, handle, placementId);
        }

        public void NativeShow(int handle, int template, float x, float y, float width, float height, string styleJson)
        {
            _bridge.CallStatic("nativeShow", Activity, handle, template, x, y, width, height, Screen.width, Screen.height, styleJson);
        }

        public void NativeHide(int handle)
        {
            _bridge.CallStatic("nativeHide", handle);
        }

        public void NativeDestroy(int handle)
        {
            _bridge.CallStatic("nativeDestroy", handle);
        }

        public int TrackingAuthorizationStatus { get { return -1; } }

        public void RequestTrackingAuthorization(int handle)
        {
            AdfactenRuntime.PostEvent(handle, "tracking", "{\"status\":-1}");
        }

        /// <summary>Receives every bridge event (Android UI thread) and queues it for the main thread.</summary>
        private sealed class Listener : AndroidJavaProxy
        {
            public Listener() : base(ListenerInterface) { }

            [Preserve]
            public void onEvent(int handle, string name, string payload)
            {
                AdfactenRuntime.PostEvent(handle, name, payload);
            }
        }
    }
}
#endif

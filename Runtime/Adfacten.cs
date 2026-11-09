using System;
using System.Collections.Generic;
using AdfactenAds.Internal;
using UnityEngine;

namespace AdfactenAds
{
    /// <summary>App Tracking Transparency status (iOS). Other platforms report <see cref="NotSupported"/>.</summary>
    public enum TrackingAuthorizationStatus
    {
        NotSupported = -1,
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3,
    }

    /// <summary>
    /// Entry point of the Adfacten Ads plugin. Wraps the native Adfacten SDKs for Android and iOS; in the Editor
    /// (and on other platforms in development builds) a mock shows placeholder ads.
    /// All callbacks and events run on the Unity main thread.
    /// </summary>
    public static class Adfacten
    {
        /// <summary>Version of this Unity package.</summary>
        public const string PluginVersion = "1.0.0";

        /// <summary>Version of the native SDKs this package is built for.</summary>
        public const string NativeSdkVersion = "1.0.2";

        internal const string WrapperName = "unity";

        /// <summary>Version reported by the native SDK at runtime (<c>"mock"</c> in the Editor).</summary>
        public static string SdkVersion
        {
            get { return AdfactenRuntime.Client.SdkVersion; }
        }

        /// <summary>Whether <see cref="Initialize(string, AdfactenConfig)"/> succeeded.</summary>
        public static bool IsInitialized
        {
            get { return AdfactenRuntime.Client.IsInitialized; }
        }

        /// <summary>
        /// Initializes the SDK with the app key for the current platform from Project Settings &gt; Adfacten.
        /// Call once, early (for example in the first scene's <c>Awake</c>).
        /// </summary>
        public static void Initialize(AdfactenConfig config = null)
        {
            var key = AppKeyForCurrentPlatform(AdfactenSettings.Instance);
            if (string.IsNullOrEmpty(key))
            {
                AdLog.Error("No app key for this platform. Set it in Project Settings > Adfacten, or call Adfacten.Initialize(appKey).");
                return;
            }
            Initialize(key, config);
        }

        /// <summary>
        /// Initializes the SDK. Call once, early; calling it again replaces the configuration.
        /// </summary>
        /// <param name="appKey">The public app key (<c>apk_…</c>) from the Adfacten panel. Android and iOS apps have different keys.</param>
        /// <param name="config">Options; null uses Project Settings &gt; Adfacten.</param>
        public static void Initialize(string appKey, AdfactenConfig config = null)
        {
            AdfactenRuntime.EnsureStarted();
            if (string.IsNullOrEmpty(appKey) || appKey.Trim().Length == 0)
            {
                AdLog.Error("Initialize: the app key is empty.");
                return;
            }
            appKey = appKey.Trim();
            if (!appKey.StartsWith("apk_", StringComparison.Ordinal))
                AdLog.Warn("\"" + appKey + "\" does not look like an Adfacten app key (apk_…).");
            var resolved = (config ?? new AdfactenConfig()).Resolve(AdfactenSettings.Instance, Debug.isDebugBuild);
            var client = AdfactenRuntime.Client;
            client.SetWrapper(WrapperName, PluginVersion);
            client.Initialize(appKey, resolved);
        }

        /// <summary>
        /// Sets (or clears, with null) your user id for rewarded ads. It is echoed in the server-side reward callback.
        /// </summary>
        public static void SetUserId(string userId)
        {
            AdfactenRuntime.Client.SetUserId(string.IsNullOrEmpty(userId) ? null : userId);
        }

        /// <summary>The App Tracking Transparency status on iOS 14+; <see cref="TrackingAuthorizationStatus.NotSupported"/> elsewhere.</summary>
        public static TrackingAuthorizationStatus TrackingAuthorizationStatus
        {
            get { return ToTrackingStatus(AdfactenRuntime.Client.TrackingAuthorizationStatus); }
        }

        /// <summary>
        /// Shows the App Tracking Transparency prompt on iOS (once per install; later calls return the stored answer).
        /// Needs a usage description in Project Settings &gt; Adfacten. The SDK never shows the prompt by itself.
        /// Other platforms call back with <see cref="TrackingAuthorizationStatus.NotSupported"/>.
        /// </summary>
        public static void RequestTrackingAuthorization(Action<TrackingAuthorizationStatus> callback)
        {
            AdfactenRuntime.EnsureStarted();
            var handle = AdfactenRuntime.NewHandle();
            AdfactenRuntime.Router.Register(handle, new TrackingRequest(handle, callback));
            AdfactenRuntime.Client.RequestTrackingAuthorization(handle);
        }

        internal static string AppKeyForCurrentPlatform(AdfactenSettings settings)
        {
#if UNITY_EDITOR
            // The Editor runs the mock; use the key of the selected build target.
#if UNITY_IOS
            return settings.AppKeyFor(RuntimePlatform.IPhonePlayer);
#else
            return settings.AppKeyFor(RuntimePlatform.Android);
#endif
#else
            return settings.AppKeyFor(Application.platform);
#endif
        }

        internal static TrackingAuthorizationStatus ToTrackingStatus(int value)
        {
            switch (value)
            {
                case 0: return TrackingAuthorizationStatus.NotDetermined;
                case 1: return TrackingAuthorizationStatus.Restricted;
                case 2: return TrackingAuthorizationStatus.Denied;
                case 3: return TrackingAuthorizationStatus.Authorized;
                default: return TrackingAuthorizationStatus.NotSupported;
            }
        }

        private sealed class TrackingRequest : IAdEventTarget
        {
            private readonly int _handle;
            private readonly Action<TrackingAuthorizationStatus> _callback;

            public TrackingRequest(int handle, Action<TrackingAuthorizationStatus> callback)
            {
                _handle = handle;
                _callback = callback;
            }

            public void HandleEvent(string name, Dictionary<string, object> payload)
            {
                if (name != "tracking") return;
                AdfactenRuntime.Router.Unregister(_handle);
                if (_callback != null) _callback(ToTrackingStatus(FlatJson.GetInt(payload, "status", -1)));
            }
        }
    }
}

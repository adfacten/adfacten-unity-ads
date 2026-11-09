using UnityEngine;

namespace AdfactenAds
{
    /// <summary>How the iOS build gets the native AdfactenAds SDK.</summary>
    public enum IosDependencyMode
    {
        /// <summary>
        /// CocoaPods through the External Dependency Manager: <c>pod 'AdfactenAds', :git =&gt; …, :tag =&gt; '1.0.2'</c>.
        /// If the build has no Podfile (no External Dependency Manager), the XCFramework is vendored instead.
        /// </summary>
        CocoaPods = 0,

        /// <summary>Download the prebuilt XCFramework from the Adfacten CDN (checksum verified) and embed it.</summary>
        XCFramework = 1,
    }

    /// <summary>
    /// Project-wide settings, edited in Project Settings &gt; Adfacten and stored in
    /// <c>Assets/Adfacten/Resources/AdfactenSettings.asset</c>.
    /// </summary>
    public sealed class AdfactenSettings : ScriptableObject
    {
        public const string ResourceName = "AdfactenSettings";

        [SerializeField, Tooltip("App key (apk_…) of your Android app in the Adfacten panel.")]
        private string androidAppKey = "";

        [SerializeField, Tooltip("App key (apk_…) of your iOS app in the Adfacten panel.")]
        private string iosAppKey = "";

        [SerializeField, Tooltip("Request test ads (never billed) in development builds and in the Editor.")]
        private bool testModeInDevelopmentBuilds = true;

        [SerializeField, Tooltip("The app is directed at children (COPPA / Google Families). Adfacten does not serve ads to such traffic.")]
        private bool childDirected;

        [SerializeField, Tooltip("Native SDK log level.")]
        private LogLevel logLevel = LogLevel.Warning;

        [SerializeField, Tooltip("Android: add com.adfacten:ads-video (VAST video for interstitial and rewarded). Needs minimum API level 23.")]
        private bool androidVideo = true;

        [SerializeField, Tooltip("Android: add com.adfacten:ads-gaid (advertising ID and App Set ID). Adds the AD_ID permission.")]
        private bool androidAdvertisingId = true;

        [SerializeField, Tooltip("Android: remove the AD_ID permission from the merged manifest (child-directed apps).")]
        private bool androidRemoveAdIdPermission;

        [SerializeField, Tooltip("Android: add the Adfacten Maven repository and artifacts to the Gradle project when the External Dependency Manager did not.")]
        private bool androidGradleFallback = true;

        [SerializeField, Tooltip("iOS: how the native SDK is added to the Xcode project.")]
        private IosDependencyMode iosDependency = IosDependencyMode.CocoaPods;

        [SerializeField, Tooltip("iOS: NSUserTrackingUsageDescription for the App Tracking Transparency prompt. Leave empty if you don't ask for tracking.")]
        private string iosUserTrackingUsageDescription = "";

        private static AdfactenSettings _cached;

        public string AndroidAppKey { get { return androidAppKey; } set { androidAppKey = value; } }
        public string IosAppKey { get { return iosAppKey; } set { iosAppKey = value; } }
        public bool TestModeInDevelopmentBuilds { get { return testModeInDevelopmentBuilds; } set { testModeInDevelopmentBuilds = value; } }
        public bool ChildDirected { get { return childDirected; } set { childDirected = value; } }
        public LogLevel LogLevel { get { return logLevel; } set { logLevel = value; } }
        public bool AndroidVideo { get { return androidVideo; } set { androidVideo = value; } }
        public bool AndroidAdvertisingId { get { return androidAdvertisingId; } set { androidAdvertisingId = value; } }
        public bool AndroidRemoveAdIdPermission { get { return androidRemoveAdIdPermission; } set { androidRemoveAdIdPermission = value; } }
        public bool AndroidGradleFallback { get { return androidGradleFallback; } set { androidGradleFallback = value; } }
        public IosDependencyMode IosDependency { get { return iosDependency; } set { iosDependency = value; } }
        public string IosUserTrackingUsageDescription { get { return iosUserTrackingUsageDescription; } set { iosUserTrackingUsageDescription = value; } }

        /// <summary>The app key for the platform the game runs on (Android or iOS), or null.</summary>
        public string AppKeyFor(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android: return Trimmed(androidAppKey);
                case RuntimePlatform.IPhonePlayer: return Trimmed(iosAppKey);
                default: return null;
            }
        }

        /// <summary>The settings asset, or defaults when the project has none.</summary>
        public static AdfactenSettings Instance
        {
            get
            {
                if (_cached == null)
                {
                    _cached = Resources.Load<AdfactenSettings>(ResourceName);
                    if (_cached == null) _cached = CreateInstance<AdfactenSettings>();
                }
                return _cached;
            }
        }

        internal static void ResetCache()
        {
            _cached = null;
        }

        private static string Trimmed(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }
    }
}

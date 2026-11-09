using System.Collections.Generic;
using System.Text;

namespace AdfactenAds.Editor
{
    /// <summary>Native SDK coordinates used by the build steps.</summary>
    internal static class AdfactenNativeDependencies
    {
        public const string Version = "1.0.2";
        public const string MavenRepository = "https://cdn.adfacten.com/maven/";
        public const string Core = "com.adfacten:ads";
        public const string Video = "com.adfacten:ads-video";
        public const string AdvertisingId = "com.adfacten:ads-gaid";

        public const string PodName = "AdfactenAds";
        public const string PodGitUrl = "https://github.com/adfacten/adfacten-ios-sdk.git";
        public const string XcframeworkUrl = "https://cdn.adfacten.com/ios/AdfactenAds-1.0.2.xcframework.zip";
        public const string XcframeworkSha256 = "a7ce159e07b4146aa2925d9c7be76dfcda2dc4a80bd9ec4ffcef24531bc9ce7d";
        public const string MinimumIosVersion = "15.0";

        public const string OptionalDependenciesPath = "Assets/Adfacten/Editor/AdfactenOptionalDependencies.xml";

        /// <summary>The Android artifacts (group:name) the project needs with these settings.</summary>
        public static List<string> AndroidArtifacts(bool video, bool advertisingId)
        {
            var list = new List<string> { Core };
            if (video) list.Add(Video);
            if (advertisingId) list.Add(AdvertisingId);
            return list;
        }

        /// <summary>The EDM4U file for the optional artifacts, or null when none is selected.</summary>
        public static string OptionalDependenciesXml(bool video, bool advertisingId)
        {
            if (!video && !advertisingId) return null;
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            sb.Append("<!-- Written by Project Settings > Adfacten. Change the options there instead of editing this file. -->\n");
            sb.Append("<dependencies>\n  <androidPackages>\n    <repositories>\n");
            sb.Append("      <repository>").Append(MavenRepository).Append("</repository>\n");
            sb.Append("    </repositories>\n");
            if (video) sb.Append("    <androidPackage spec=\"").Append(Video).Append(':').Append(Version).Append("\" />\n");
            if (advertisingId) sb.Append("    <androidPackage spec=\"").Append(AdvertisingId).Append(':').Append(Version).Append("\" />\n");
            sb.Append("  </androidPackages>\n</dependencies>\n");
            return sb.ToString();
        }
    }
}

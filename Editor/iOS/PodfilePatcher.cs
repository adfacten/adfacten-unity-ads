using System.Text.RegularExpressions;

namespace AdfactenAds.Editor
{
    /// <summary>Edits the Podfile written by the External Dependency Manager. No Unity APIs, so it is unit tested.</summary>
    internal static class PodfilePatcher
    {
        private static readonly Regex PodLine = new Regex("^([ \\t]*)pod\\s+['\"]AdfactenAds['\"][^\\n]*$", RegexOptions.Multiline);

        public static bool HasPod(string podfile)
        {
            return podfile != null && PodLine.IsMatch(podfile);
        }

        /// <summary>Points the AdfactenAds pod at the source podspec on GitHub (it is not on the CocoaPods trunk).</summary>
        public static string UseGitSource(string podfile)
        {
            var line = "pod '" + AdfactenNativeDependencies.PodName + "', :git => '" + AdfactenNativeDependencies.PodGitUrl +
                       "', :tag => '" + AdfactenNativeDependencies.Version + "'";
            return PodLine.Replace(podfile, m => m.Groups[1].Value + line);
        }

        /// <summary>Removes the AdfactenAds pod (the XCFramework is embedded instead).</summary>
        public static string RemovePod(string podfile)
        {
            return Regex.Replace(podfile, "^[ \\t]*pod\\s+['\"]AdfactenAds['\"][^\\n]*\\n?", "", RegexOptions.Multiline);
        }
    }
}

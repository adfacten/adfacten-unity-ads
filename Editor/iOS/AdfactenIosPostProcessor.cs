#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace AdfactenAds.Editor
{
    /// <summary>
    /// Prepares the Xcode project: the AdfactenAds dependency (CocoaPods source pod or the embedded XCFramework),
    /// system frameworks, iOS 15 deployment target and the optional ATT usage description. The SDK's privacy
    /// manifest ships inside the framework (or the pod's resource bundle); nothing is copied into the app.
    /// </summary>
    internal static class AdfactenIosPostProcessor
    {
        // The External Dependency Manager writes the Podfile at order 40 and runs "pod install" at 50.
        internal const int Order = 45;

        private const string FrameworksDir = "Frameworks/Adfacten";

        private static readonly string[] SystemFrameworks =
        {
            "WebKit.framework", "AVFoundation.framework", "StoreKit.framework", "AdSupport.framework",
            "AppTrackingTransparency.framework", "Network.framework", "SwiftUI.framework",
        };

        [PostProcessBuild(Order)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;
            var settings = AdfactenSettingsAsset.LoadOrDefault();
            var usesPod = PatchPodfile(buildPath, settings.IosDependency);

            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();

            if (!usesPod) EmbedXcframework(project, buildPath, mainTarget, frameworkTarget);
            foreach (var framework in SystemFrameworks) project.AddFrameworkToProject(frameworkTarget, framework, false);
            project.SetBuildProperty(frameworkTarget, "CLANG_ENABLE_MODULES", "YES");
            RaiseDeploymentTarget(project, project.ProjectGuid());
            RaiseDeploymentTarget(project, mainTarget);
            RaiseDeploymentTarget(project, frameworkTarget);
            project.WriteToFile(projectPath);

            var usage = (settings.IosUserTrackingUsageDescription ?? "").Trim();
            if (usage.Length > 0)
            {
                var plistPath = Path.Combine(buildPath, "Info.plist");
                var plist = new PlistDocument();
                plist.ReadFromFile(plistPath);
                plist.root.SetString("NSUserTrackingUsageDescription", usage);
                plist.WriteToFile(plistPath);
            }
        }

        /// <summary>
        /// Points the EDM4U Podfile at the source pod, or removes the pod in XCFramework mode. Returns whether the
        /// SDK comes from CocoaPods.
        /// </summary>
        private static bool PatchPodfile(string buildPath, IosDependencyMode mode)
        {
            var podfile = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfile)) return false;
            var text = File.ReadAllText(podfile);
            if (!PodfilePatcher.HasPod(text)) return false;
            if (mode == IosDependencyMode.CocoaPods)
            {
                File.WriteAllText(podfile, PodfilePatcher.UseGitSource(text));
                return true;
            }
            File.WriteAllText(podfile, PodfilePatcher.RemovePod(text));
            return false;
        }

        private static void EmbedXcframework(PBXProject project, string buildPath, string mainTarget, string frameworkTarget)
        {
            string zip;
            var cache = Path.Combine(Directory.GetCurrentDirectory(), "Library/Adfacten");
            try
            {
                zip = XcframeworkInstaller.EnsureZip(cache, AdfactenNativeDependencies.XcframeworkUrl, AdfactenNativeDependencies.XcframeworkSha256);
            }
            catch (Exception e)
            {
                throw new BuildFailedException("[Adfacten] Could not get " + AdfactenNativeDependencies.XcframeworkUrl + " (" + e.Message +
                                               "). To build offline, put the zip in " + cache + ".");
            }
            XcframeworkInstaller.Extract(zip, Path.Combine(buildPath, FrameworksDir));

            var relative = FrameworksDir + "/" + XcframeworkInstaller.FrameworkName;
            var fileGuid = project.FindFileGuidByProjectPath(relative);
            if (fileGuid == null)
            {
                fileGuid = project.AddFile(relative, relative, PBXSourceTree.Source);
                project.AddFileToBuildSection(frameworkTarget, project.GetFrameworksBuildPhaseByTarget(frameworkTarget), fileGuid);
                // A dynamic framework: the app embeds and signs it, UnityFramework links it.
                project.AddFileToEmbedFrameworks(mainTarget, fileGuid);
            }
            project.AddBuildProperty(frameworkTarget, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            Debug.Log("[Adfacten] Embedded AdfactenAds " + AdfactenNativeDependencies.Version + " (XCFramework, checksum verified).");
        }

        private static void RaiseDeploymentTarget(PBXProject project, string targetGuid)
        {
            var current = project.GetBuildPropertyForAnyConfig(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET");
            Version v;
            if (current != null && Version.TryParse(current, out v) && v >= new Version(AdfactenNativeDependencies.MinimumIosVersion)) return;
            project.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", AdfactenNativeDependencies.MinimumIosVersion);
        }
    }
}
#endif

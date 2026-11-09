using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AdfactenAds.Editor
{
    /// <summary>Checks the project before an Android or iOS build and stops on settings that cannot work.</summary>
    internal sealed class AdfactenBuildChecks : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.Android && target != BuildTarget.iOS) return;
            var settings = AdfactenSettingsAsset.LoadOrDefault();
            AdfactenSettingsAsset.SyncOptionalDependencies(settings);

            var input = new Input
            {
                Android = target == BuildTarget.Android,
                AppKey = target == BuildTarget.Android ? settings.AndroidAppKey : settings.IosAppKey,
                AndroidMinSdk = (int)PlayerSettings.Android.minSdkVersion,
                IosTargetVersion = PlayerSettings.iOS.targetOSVersionString,
                HasEdm4u = HasExternalDependencyManager(),
                Settings = settings,
            };
            foreach (var issue in Evaluate(input))
            {
                if (issue.Fatal) throw new BuildFailedException("[Adfacten] " + issue.Message);
                Debug.LogWarning("[Adfacten] " + issue.Message);
            }
        }

        internal struct Input
        {
            public bool Android;
            public string AppKey;
            public int AndroidMinSdk;
            public string IosTargetVersion;
            public bool HasEdm4u;
            public AdfactenSettings Settings;
        }

        internal struct Issue
        {
            public bool Fatal;
            public string Message;
        }

        internal static List<Issue> Evaluate(Input input)
        {
            var issues = new List<Issue>();
            var s = input.Settings;
            var platform = input.Android ? "Android" : "iOS";
            if (string.IsNullOrEmpty(input.AppKey) || input.AppKey.Trim().Length == 0)
                issues.Add(Warn("No " + platform + " app key in Project Settings > Adfacten. Pass it to Adfacten.Initialize(appKey) instead."));

            if (input.Android)
            {
                // AndroidSdkVersions.AndroidApiLevelAuto (0) means Unity's lowest supported level.
                var minSdk = input.AndroidMinSdk;
                if (s.AndroidVideo && minSdk > 0 && minSdk < 23)
                {
                    issues.Add(new Issue
                    {
                        Fatal = true,
                        Message = "Video ads (com.adfacten:ads-video) need Minimum API Level 23. Raise it in Player Settings > Android > " +
                                  "Other Settings, or turn off Video ads in Project Settings > Adfacten.",
                    });
                }
                if (s.ChildDirected && s.AndroidAdvertisingId && !s.AndroidRemoveAdIdPermission)
                    issues.Add(Warn("The app is child-directed but includes ads-gaid (AD_ID permission). Turn off Advertising ID or remove the AD_ID permission in Project Settings > Adfacten."));
                if (!input.HasEdm4u && !s.AndroidGradleFallback)
                    issues.Add(Warn("The External Dependency Manager is not installed and the Gradle fallback is off: add com.adfacten:ads:1.0.2 to your Gradle templates yourself (see the README)."));
            }
            else
            {
                Version v;
                if (Version.TryParse(input.IosTargetVersion ?? "", out v) && v < new Version(15, 0))
                    issues.Add(Warn("The Adfacten iOS SDK needs iOS 15.0. The Xcode project's deployment target is raised to 15.0; set Target minimum iOS Version to 15.0 in Player Settings to make it explicit."));
            }
            return issues;
        }

        internal static bool HasExternalDependencyManager()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType("GooglePlayServices.PlayServicesResolver", false) != null) return true;
                }
                catch (Exception)
                {
                    // Some dynamic assemblies cannot be searched.
                }
            }
            return false;
        }

        private static Issue Warn(string message)
        {
            return new Issue { Fatal = false, Message = message };
        }
    }
}

#if UNITY_ANDROID
using System.Collections.Generic;
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace AdfactenAds.Editor
{
    /// <summary>
    /// After Unity generates the Gradle project: adds the Adfacten artifacts when the External Dependency Manager
    /// did not (no EDM4U, or EDM4U without Gradle templates), and removes the AD_ID permission when asked to.
    /// </summary>
    internal sealed class AdfactenGradlePostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 100; } }

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var settings = AdfactenSettingsAsset.LoadOrDefault();
            var root = Directory.GetParent(unityLibraryPath).FullName;
            if (settings.AndroidGradleFallback) AddMissingDependencies(root, unityLibraryPath, settings);
            if (settings.AndroidRemoveAdIdPermission)
            {
                var manifest = Path.Combine(unityLibraryPath, "src/main/AndroidManifest.xml");
                if (File.Exists(manifest)) File.WriteAllText(manifest, GradlePatcher.RemoveAdIdPermission(File.ReadAllText(manifest)));
            }
        }

        internal static void AddMissingDependencies(string root, string unityLibraryPath, AdfactenSettings settings)
        {
            var buildGradle = Path.Combine(unityLibraryPath, "build.gradle");
            if (!File.Exists(buildGradle)) return;
            var gradle = File.ReadAllText(buildGradle);
            var missing = new List<string>();
            foreach (var artifact in AdfactenNativeDependencies.AndroidArtifacts(settings.AndroidVideo, settings.AndroidAdvertisingId))
            {
                if (GradlePatcher.HasDependency(gradle, artifact) || HasLocalAar(unityLibraryPath, artifact)) continue;
                missing.Add(artifact + ":" + AdfactenNativeDependencies.Version);
            }
            if (missing.Count == 0) return;

            var repoAdded = false;
            foreach (var file in new[] { Path.Combine(root, "settings.gradle"), Path.Combine(root, "build.gradle") })
            {
                if (!File.Exists(file)) continue;
                var patched = GradlePatcher.AddRepository(File.ReadAllText(file), AdfactenNativeDependencies.MavenRepository);
                if (patched == null) continue;
                File.WriteAllText(file, patched);
                repoAdded = true;
                break;
            }
            if (!repoAdded)
            {
                Debug.LogWarning("[Adfacten] Could not find a repositories block for " + AdfactenNativeDependencies.MavenRepository +
                                 "; add it to your Gradle templates (see the README).");
            }
            File.WriteAllText(buildGradle, GradlePatcher.AddDependencies(gradle, missing));
            Debug.Log("[Adfacten] Added to the Gradle project: " + string.Join(", ", missing.ToArray()) +
                      ". Install the External Dependency Manager to resolve them with your other plugins instead.");
        }

        /// <summary>EDM4U in "copy AARs" mode puts files such as libs/com.adfacten.ads-1.0.2.aar in the project.</summary>
        private static bool HasLocalAar(string unityLibraryPath, string artifact)
        {
            var libs = Path.Combine(unityLibraryPath, "libs");
            if (!Directory.Exists(libs)) return false;
            var prefix = artifact.Replace(':', '.') + "-";
            foreach (var f in Directory.GetFiles(libs))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith(prefix) && (name.EndsWith(".aar") || name.EndsWith(".jar"))) return true;
            }
            return false;
        }
    }
}
#endif

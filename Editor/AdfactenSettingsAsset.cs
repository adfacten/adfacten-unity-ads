using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdfactenAds.Editor
{
    /// <summary>Creates and loads the settings asset, and keeps the optional EDM4U file in line with it.</summary>
    [InitializeOnLoad]
    internal static class AdfactenSettingsAsset
    {
        public const string Directory = "Assets/Adfacten/Resources";
        public const string Path = Directory + "/" + AdfactenSettings.ResourceName + ".asset";

        static AdfactenSettingsAsset()
        {
            // After scripts reload, so the AssetDatabase is ready.
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) SyncOptionalDependencies(LoadOrDefault());
            };
        }

        /// <summary>The project's settings asset, created on first use.</summary>
        public static AdfactenSettings LoadOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AdfactenSettings>(Path);
            if (settings != null) return settings;
            System.IO.Directory.CreateDirectory(Directory);
            settings = ScriptableObject.CreateInstance<AdfactenSettings>();
            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>The settings asset, or defaults (not saved) when the project has none.</summary>
        public static AdfactenSettings LoadOrDefault()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AdfactenSettings>(Path);
            return settings != null ? settings : ScriptableObject.CreateInstance<AdfactenSettings>();
        }

        /// <summary>Writes (or removes) Assets/Adfacten/Editor/AdfactenOptionalDependencies.xml for EDM4U.</summary>
        public static void SyncOptionalDependencies(AdfactenSettings settings)
        {
            var path = AdfactenNativeDependencies.OptionalDependenciesPath;
            var xml = AdfactenNativeDependencies.OptionalDependenciesXml(settings.AndroidVideo, settings.AndroidAdvertisingId);
            if (xml == null)
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                return;
            }
            if (File.Exists(path) && File.ReadAllText(path) == xml) return;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, xml);
            AssetDatabase.ImportAsset(path);
        }
    }
}

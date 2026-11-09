using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AdfactenAds.Editor
{
    /// <summary>Project Settings &gt; Adfacten.</summary>
    internal sealed class AdfactenSettingsProvider : SettingsProvider
    {
        private SerializedObject _settings;

        private AdfactenSettingsProvider() : base("Project/Adfacten", SettingsScope.Project, new[] { "Adfacten", "ads", "app key" }) { }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new AdfactenSettingsProvider();
        }

        public override void OnGUI(string searchContext)
        {
            if (_settings == null || _settings.targetObject == null)
                _settings = new SerializedObject(AdfactenSettingsAsset.LoadOrCreate());
            _settings.Update();
            EditorGUIUtility.labelWidth = 260;

            EditorGUILayout.LabelField("App keys", EditorStyles.boldLabel);
            Field("androidAppKey", "Android app key");
            Field("iosAppKey", "iOS app key");
            EditorGUILayout.HelpBox("Keys (apk_…) and placement ids (plc_…) are in the Adfacten panel under Apps. " +
                                    "The server checks the app's bundle id / package name against the key.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Requests", EditorStyles.boldLabel);
            Field("testModeInDevelopmentBuilds", "Test mode in development builds");
            Field("childDirected", "Child-directed (COPPA)");
            Field("logLevel", "Native log level");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Android", EditorStyles.boldLabel);
            Field("androidVideo", "Video ads (ads-video, API 23+)");
            Field("androidAdvertisingId", "Advertising ID (ads-gaid, AD_ID permission)");
            Field("androidRemoveAdIdPermission", "Remove AD_ID permission");
            Field("androidGradleFallback", "Add Gradle dependencies without EDM4U");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("iOS", EditorStyles.boldLabel);
            Field("iosDependency", "iOS dependency");
            Field("iosUserTrackingUsageDescription", "Tracking usage description (ATT)");

            var s = (AdfactenSettings)_settings.targetObject;
            foreach (var warning in Warnings(s)) EditorGUILayout.HelpBox(warning, MessageType.Warning);

            if (_settings.ApplyModifiedProperties())
            {
                AdfactenSettings.ResetCache();
                AdfactenSettingsAsset.SyncOptionalDependencies(s);
            }
        }

        internal static IEnumerable<string> Warnings(AdfactenSettings s)
        {
            if (s.ChildDirected && s.AndroidAdvertisingId)
                yield return "Child-directed apps should not read the advertising ID: turn off Advertising ID (or remove the AD_ID permission).";
            if (s.AndroidRemoveAdIdPermission && s.AndroidAdvertisingId && !s.ChildDirected)
                yield return "The AD_ID permission is removed, so the advertising ID cannot be read on Android 13+.";
            if (!string.IsNullOrEmpty(s.AndroidAppKey) && !s.AndroidAppKey.Trim().StartsWith("apk_"))
                yield return "The Android app key should start with apk_.";
            if (!string.IsNullOrEmpty(s.IosAppKey) && !s.IosAppKey.Trim().StartsWith("apk_"))
                yield return "The iOS app key should start with apk_.";
        }

        private void Field(string property, string label)
        {
            var p = _settings.FindProperty(property);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip));
        }
    }
}

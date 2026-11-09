using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace AdfactenAds.Tests
{
    public class ConfigTests
    {
        private static AdfactenSettings Settings()
        {
            return ScriptableObject.CreateInstance<AdfactenSettings>();
        }

        [Test]
        public void TestModeFollowsDevelopmentBuildsByDefault()
        {
            var s = Settings();
            Assert.IsTrue(new AdfactenConfig().Resolve(s, true).TestMode);
            Assert.IsFalse(new AdfactenConfig().Resolve(s, false).TestMode);
            s.TestModeInDevelopmentBuilds = false;
            Assert.IsFalse(new AdfactenConfig().Resolve(s, true).TestMode);
        }

        [Test]
        public void ExplicitValuesWin()
        {
            var s = Settings();
            s.ChildDirected = true;
            s.LogLevel = LogLevel.Debug;
            var r = new AdfactenConfig { TestMode = false, ChildDirected = false, LogLevel = LogLevel.None, UserId = "u1" }.Resolve(s, true);
            Assert.IsFalse(r.TestMode);
            Assert.IsFalse(r.ChildDirected);
            Assert.AreEqual(LogLevel.None, r.LogLevel);
            Assert.AreEqual("u1", r.UserId);
            Assert.IsTrue(new AdfactenConfig { TestMode = true }.Resolve(s, false).TestMode);
        }

        [Test]
        public void UnsetValuesComeFromSettings()
        {
            var s = Settings();
            Assert.AreEqual(LogLevel.Warning, new AdfactenConfig().Resolve(s, true).LogLevel);
            Assert.IsFalse(new AdfactenConfig().Resolve(s, true).ChildDirected);
            s.ChildDirected = true;
            Assert.IsTrue(new AdfactenConfig().Resolve(s, true).ChildDirected);
            Assert.IsNull(new AdfactenConfig { UserId = "" }.Resolve(s, true).UserId);
        }

        [Test]
        public void LogLevelsMatchTheNativeSdks()
        {
            // Android LogLevel NONE..DEBUG and iOS AdfactenLogLevel none..debug are both 0..4.
            Assert.AreEqual(0, (int)LogLevel.None);
            Assert.AreEqual(1, (int)LogLevel.Error);
            Assert.AreEqual(2, (int)LogLevel.Warning);
            Assert.AreEqual(3, (int)LogLevel.Info);
            Assert.AreEqual(4, (int)LogLevel.Debug);
        }

        [Test]
        public void AppKeyPerPlatform()
        {
            var s = Settings();
            s.AndroidAppKey = "  apk_android ";
            s.IosAppKey = "";
            Assert.AreEqual("apk_android", s.AppKeyFor(RuntimePlatform.Android));
            Assert.IsNull(s.AppKeyFor(RuntimePlatform.IPhonePlayer));
            Assert.IsNull(s.AppKeyFor(RuntimePlatform.WebGLPlayer));
            s.IosAppKey = "apk_ios";
            Assert.AreEqual("apk_ios", s.AppKeyFor(RuntimePlatform.IPhonePlayer));
        }

        [Test]
        public void DefaultSettings()
        {
            var s = Settings();
            Assert.IsTrue(s.TestModeInDevelopmentBuilds);
            Assert.IsFalse(s.ChildDirected);
            Assert.IsTrue(s.AndroidVideo);
            Assert.IsTrue(s.AndroidAdvertisingId);
            Assert.IsFalse(s.AndroidRemoveAdIdPermission);
            Assert.IsTrue(s.AndroidGradleFallback);
            Assert.AreEqual(IosDependencyMode.CocoaPods, s.IosDependency);
        }

        [Test]
        public void BannerSizesAndPositionsMatchTheBridges()
        {
            // Plugins/Android UnityBanner and Plugins/iOS AdfactenUnityBridge.mm use these numbers.
            Assert.AreEqual(0, AdSize.Banner.Index);
            Assert.AreEqual(1, AdSize.LargeBanner.Index);
            Assert.AreEqual(2, AdSize.MediumRectangle.Index);
            Assert.AreEqual(3, AdSize.Leaderboard.Index);
            Assert.AreEqual("728x90", AdSize.Leaderboard.ToString());
            Assert.AreEqual(0, (int)AdPosition.Top.Type);
            Assert.AreEqual(1, (int)AdPosition.Bottom.Type);
            Assert.AreEqual(2, (int)AdPosition.Center.Type);
            Assert.AreEqual(6, (int)AdPosition.BottomRight.Type);
            var custom = AdPosition.Custom(10, 20);
            Assert.AreEqual(7, (int)custom.Type);
            Assert.AreEqual(10f, custom.X);
            Assert.AreEqual(20f, custom.Y);
        }

        [Test]
        public void PluginVersionMatchesPackageJson()
        {
            var root = TestEnv.PackageRoot();
            if (root == null) Assert.Ignore("package folder not found");
            var json = File.ReadAllText(Path.Combine(root, "package.json"));
            var m = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            Assert.IsTrue(m.Success);
            Assert.AreEqual(m.Groups[1].Value, Adfacten.PluginVersion);
        }
    }
}

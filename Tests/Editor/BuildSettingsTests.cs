using System.IO;
using System.Xml;
using AdfactenAds.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AdfactenAds.Tests
{
    public class BuildSettingsTests
    {
        [Test]
        public void OptionalDependenciesFile()
        {
            Assert.IsNull(AdfactenNativeDependencies.OptionalDependenciesXml(false, false));
            var both = AdfactenNativeDependencies.OptionalDependenciesXml(true, true);
            StringAssert.Contains("spec=\"com.adfacten:ads-video:1.0.2\"", both);
            StringAssert.Contains("spec=\"com.adfacten:ads-gaid:1.0.2\"", both);
            StringAssert.Contains("<repository>https://cdn.adfacten.com/maven/</repository>", both);
            var videoOnly = AdfactenNativeDependencies.OptionalDependenciesXml(true, false);
            StringAssert.DoesNotContain("ads-gaid", videoOnly);
            Assert.DoesNotThrow(() => new XmlDocument().LoadXml(both));
            CollectionAssert.AreEqual(new[] { "com.adfacten:ads", "com.adfacten:ads-gaid" }, AdfactenNativeDependencies.AndroidArtifacts(false, true));
        }

        [Test]
        public void PackageDependenciesXml()
        {
            var root = TestEnv.PackageRoot();
            if (root == null) Assert.Ignore("package folder not found");
            var doc = new XmlDocument();
            doc.Load(Path.Combine(root, "Editor/AdfactenDependencies.xml"));
            Assert.AreEqual("com.adfacten:ads:1.0.2", doc.SelectSingleNode("//androidPackage/@spec").Value);
            Assert.AreEqual("https://cdn.adfacten.com/maven/", doc.SelectSingleNode("//repository").InnerText.Trim());
            Assert.AreEqual("AdfactenAds", doc.SelectSingleNode("//iosPod/@name").Value);
            Assert.AreEqual("15.0", doc.SelectSingleNode("//iosPod/@minTargetSdk").Value);
        }

        private static AdfactenBuildChecks.Input Input(bool android)
        {
            return new AdfactenBuildChecks.Input
            {
                Android = android,
                AppKey = "apk_x",
                AndroidMinSdk = 23,
                IosTargetVersion = "15.0",
                HasEdm4u = true,
                Settings = ScriptableObject.CreateInstance<AdfactenSettings>(),
            };
        }

        [Test]
        public void VideoNeedsApi23()
        {
            var input = Input(true);
            input.AndroidMinSdk = 22;
            var issues = AdfactenBuildChecks.Evaluate(input);
            Assert.IsTrue(issues.Exists(i => i.Fatal));
            input.Settings.AndroidVideo = false;
            Assert.IsFalse(AdfactenBuildChecks.Evaluate(input).Exists(i => i.Fatal));
            input.Settings.AndroidVideo = true;
            input.AndroidMinSdk = 0; // automatic: Unity's lowest level is checked by Gradle
            Assert.IsFalse(AdfactenBuildChecks.Evaluate(input).Exists(i => i.Fatal));
        }

        [Test]
        public void CleanProjectHasNoIssues()
        {
            Assert.IsEmpty(AdfactenBuildChecks.Evaluate(Input(true)));
            Assert.IsEmpty(AdfactenBuildChecks.Evaluate(Input(false)));
        }

        [Test]
        public void Warnings()
        {
            var ios = Input(false);
            ios.IosTargetVersion = "13.0";
            ios.AppKey = "";
            Assert.AreEqual(2, AdfactenBuildChecks.Evaluate(ios).Count);

            var android = Input(true);
            android.Settings.ChildDirected = true;
            android.HasEdm4u = false;
            android.Settings.AndroidGradleFallback = false;
            var issues = AdfactenBuildChecks.Evaluate(android);
            Assert.AreEqual(2, issues.Count);
            Assert.IsFalse(issues.Exists(i => i.Fatal));
        }
    }
}

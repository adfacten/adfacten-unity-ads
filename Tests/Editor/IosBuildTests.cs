using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using AdfactenAds.Editor;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class IosBuildTests
    {
        private const string Podfile = @"source 'https://cdn.cocoapods.org/'
platform :ios, '15.0'

target 'UnityFramework' do
  pod 'AdfactenAds', '1.0.2'
  pod 'Other', '2.0'
end
target 'Unity-iPhone' do
end
use_frameworks! :linkage => :static
";

        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "adfacten-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void PointsThePodAtTheGitTag()
        {
            Assert.IsTrue(PodfilePatcher.HasPod(Podfile));
            var patched = PodfilePatcher.UseGitSource(Podfile);
            StringAssert.Contains("  pod 'AdfactenAds', :git => 'https://github.com/adfacten/adfacten-ios-sdk.git', :tag => '1.0.2'\n", patched);
            StringAssert.DoesNotContain("'AdfactenAds', '1.0.2'", patched);
            StringAssert.Contains("pod 'Other', '2.0'", patched);
            Assert.AreEqual(patched, PodfilePatcher.UseGitSource(patched), "idempotent");
        }

        [Test]
        public void RemovesThePodForTheXcframework()
        {
            var patched = PodfilePatcher.RemovePod(Podfile);
            Assert.IsFalse(PodfilePatcher.HasPod(patched));
            StringAssert.Contains("  pod 'Other', '2.0'\nend", patched);
            Assert.IsFalse(PodfilePatcher.HasPod("pod 'AdfactenAdsExtra', '1.0'"));
        }

        [Test]
        public void DownloadsVerifiesAndCaches()
        {
            var zip = MakeZip("AdfactenAds.xcframework/Info.plist", "AdfactenAds.xcframework/ios-arm64/AdfactenAds.framework/AdfactenAds");
            var sha = XcframeworkInstaller.Sha256Hex(zip);
            var downloads = 0;
            Func<string, byte[]> download = url => { downloads++; return zip; };
            var cache = Path.Combine(_dir, "cache");
            var path = XcframeworkInstaller.EnsureZip(cache, "https://cdn.example/x/AdfactenAds-1.0.2.xcframework.zip", sha, download);
            Assert.AreEqual("AdfactenAds-1.0.2.xcframework.zip", Path.GetFileName(path));
            XcframeworkInstaller.EnsureZip(cache, "https://cdn.example/x/AdfactenAds-1.0.2.xcframework.zip", sha, download);
            Assert.AreEqual(1, downloads, "a verified zip in the cache is reused");

            var framework = XcframeworkInstaller.Extract(path, Path.Combine(_dir, "Frameworks/Adfacten"));
            Assert.IsTrue(File.Exists(Path.Combine(framework, "Info.plist")));
            Assert.IsTrue(File.Exists(Path.Combine(framework, "ios-arm64/AdfactenAds.framework/AdfactenAds")));
            // Extracting again replaces the old copy.
            Assert.DoesNotThrow(() => XcframeworkInstaller.Extract(path, Path.Combine(_dir, "Frameworks/Adfacten")));
        }

        [Test]
        public void RejectsAChecksumMismatch()
        {
            var cache = Path.Combine(_dir, "cache");
            var wrong = new string('0', 64);
            Assert.Throws<InvalidDataException>(() =>
                XcframeworkInstaller.EnsureZip(cache, "https://cdn.example/a.zip", wrong, url => Encoding.UTF8.GetBytes("tampered")));
            Assert.IsFalse(File.Exists(Path.Combine(cache, "a.zip")), "nothing unverified is kept");
        }

        [Test]
        public void RejectsPathsOutsideTheTarget()
        {
            var zipPath = Path.Combine(_dir, "evil.zip");
            File.WriteAllBytes(zipPath, MakeZip("AdfactenAds.xcframework/Info.plist", "../escape.txt"));
            Assert.Throws<InvalidDataException>(() => XcframeworkInstaller.Extract(zipPath, Path.Combine(_dir, "out")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "escape.txt")));
        }

        [Test]
        public void ZipWithoutTheFrameworkFails()
        {
            var zipPath = Path.Combine(_dir, "other.zip");
            File.WriteAllBytes(zipPath, MakeZip("Something/Info.plist"));
            Assert.Throws<InvalidDataException>(() => XcframeworkInstaller.Extract(zipPath, Path.Combine(_dir, "out")));
        }

        /// <summary>Optional: the real CDN zip (ADFACTEN_XCFRAMEWORK_ZIP) matches the pinned checksum and unpacks.</summary>
        [Test]
        public void RealXcframeworkZip()
        {
            var real = Environment.GetEnvironmentVariable("ADFACTEN_XCFRAMEWORK_ZIP");
            if (string.IsNullOrEmpty(real) || !File.Exists(real)) Assert.Ignore("ADFACTEN_XCFRAMEWORK_ZIP not set");
            Assert.AreEqual(AdfactenNativeDependencies.XcframeworkSha256, XcframeworkInstaller.Sha256Hex(File.ReadAllBytes(real)));
            var framework = XcframeworkInstaller.Extract(real, Path.Combine(_dir, "Frameworks/Adfacten"));
            Assert.IsTrue(File.Exists(Path.Combine(framework, "ios-arm64/AdfactenAds.framework/Headers/AdfactenAds-Swift.h")));
            Assert.IsTrue(File.Exists(Path.Combine(framework, "ios-arm64/AdfactenAds.framework/PrivacyInfo.xcprivacy")), "privacy manifest inside the framework");
        }

        private static byte[] MakeZip(params string[] files)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var f in files)
                    {
                        using (var w = new StreamWriter(zip.CreateEntry(f).Open())) w.Write("x");
                    }
                }
                return ms.ToArray();
            }
        }
    }
}

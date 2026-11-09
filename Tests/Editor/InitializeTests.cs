using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class InitializeTests
    {
        [Test]
        public void ReportsTheWrapperBeforeInitializing()
        {
            using (var env = new TestEnv(false))
            {
                Adfacten.Initialize(" apk_test ", new AdfactenConfig { TestMode = true, UserId = "p1" });
                Assert.AreEqual(2, env.Mock.Calls.Count);
                Assert.AreEqual("setWrapper:unity:" + Adfacten.PluginVersion, env.Mock.Calls[0]);
                Assert.AreEqual("initialize:apk_test", env.Mock.Calls[1]);
                Assert.IsTrue(env.Mock.Config.TestMode);
                Assert.AreEqual("p1", env.Mock.UserId);
                Assert.IsTrue(Adfacten.IsInitialized);
            }
        }

        [Test]
        public void WrapperVersionFitsTheNativeRules()
        {
            // Up to 20 characters of [0-9A-Za-z.+-], or the SDK sends "unknown".
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(Adfacten.PluginVersion, "^[0-9A-Za-z.+-]{1,20}$"));
            Assert.AreEqual("unity", Adfacten.WrapperName);
        }

        [Test]
        public void EmptyKeyDoesNotInitialize()
        {
            using (var env = new TestEnv(false))
            {
                Adfacten.Initialize("  ");
                Assert.AreEqual(0, env.Mock.Calls.Count);
                Assert.IsFalse(Adfacten.IsInitialized);
                Assert.IsTrue(env.Logs.Exists(m => m.StartsWith("error:")));
            }
        }

        [Test]
        public void InitializeWithoutKeyUsesSettings()
        {
            using (var env = new TestEnv(false))
            {
                if (Adfacten.AppKeyForCurrentPlatform(AdfactenSettings.Instance) != null) Assert.Ignore("this project has an app key");
                Adfacten.Initialize();
                Assert.AreEqual(0, env.Mock.Calls.Count);
                Assert.IsTrue(env.Logs.Exists(m => m.Contains("Project Settings > Adfacten")));
            }
        }

        [Test]
        public void SetUserId()
        {
            using (var env = new TestEnv())
            {
                Adfacten.SetUserId("p2");
                Assert.AreEqual("p2", env.Mock.UserId);
                Adfacten.SetUserId("");
                Assert.IsNull(env.Mock.UserId);
            }
        }

        [Test]
        public void TrackingIsNotSupportedOffIos()
        {
            using (var env = new TestEnv())
            {
                TrackingAuthorizationStatus? result = null;
                Adfacten.RequestTrackingAuthorization(s => result = s);
                Assert.IsNull(result, "callbacks are asynchronous");
                env.Drain();
                Assert.AreEqual(TrackingAuthorizationStatus.NotSupported, result);
                Assert.AreEqual(TrackingAuthorizationStatus.NotSupported, Adfacten.TrackingAuthorizationStatus);
            }
        }
    }
}

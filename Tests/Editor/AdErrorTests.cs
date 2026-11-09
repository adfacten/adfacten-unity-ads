using System;
using System.Collections.Generic;
using AdfactenAds.Internal;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class AdErrorTests
    {
        // The shared vocabulary of the native SDKs 1.0.2.
        private static readonly string[] Names =
        {
            "internal_error", "not_initialized", "invalid_request", "network_error", "no_fill", "server_error",
            "invalid_response", "rejected", "ad_expired", "already_shown", "not_ready", "show_failed",
            "video_not_supported", "load_in_progress", "child_directed", "creative_failed",
        };

        [Test]
        public void EveryNameHasItsOwnCode()
        {
            var seen = new HashSet<AdErrorCode>();
            foreach (var name in Names)
            {
                var code = AdError.CodeOf(name);
                Assert.IsTrue(seen.Add(code), name + " maps to a code used twice");
                Assert.AreEqual(name, AdError.NameOf(code));
            }
            Assert.AreEqual(Enum.GetValues(typeof(AdErrorCode)).Length, seen.Count, "every code has a name");
        }

        [Test]
        public void UnknownNameIsInternalButKeepsTheName()
        {
            var e = AdError.FromPayload(FlatJson.Parse("{\"name\":\"brand_new\",\"message\":\"m\"}"));
            Assert.AreEqual(AdErrorCode.InternalError, e.Code);
            Assert.AreEqual("brand_new", e.Name);
        }

        [Test]
        public void ParsesBridgePayload()
        {
            var e = AdError.FromPayload(FlatJson.Parse(
                "{\"name\":\"rejected\",\"message\":\"bundle id does not match\",\"serverCode\":\"bundle_mismatch\",\"nativeCode\":7}"));
            Assert.AreEqual(AdErrorCode.Rejected, e.Code);
            Assert.AreEqual("rejected", e.Name);
            Assert.AreEqual("bundle id does not match", e.Message);
            Assert.AreEqual("bundle_mismatch", e.ServerCode);
            Assert.AreEqual(7, e.NativeCode);
            StringAssert.Contains("bundle_mismatch", e.ToString());
        }

        [Test]
        public void IosCoppaRefusalIsRejectedWithServerCode()
        {
            // iOS reports a server COPPA refusal with code childDirected (10) but the name "rejected".
            var e = AdError.FromPayload(FlatJson.Parse("{\"name\":\"rejected\",\"message\":\"coppa\",\"serverCode\":\"coppa\",\"nativeCode\":10}"));
            Assert.AreEqual(AdErrorCode.Rejected, e.Code);
            Assert.AreEqual("coppa", e.ServerCode);
        }

        [Test]
        public void MissingFieldsGetDefaults()
        {
            var e = AdError.FromPayload(FlatJson.Parse("{}"));
            Assert.AreEqual(AdErrorCode.InternalError, e.Code);
            Assert.AreEqual("internal_error", e.Name);
            Assert.AreEqual("internal_error", e.Message);
            Assert.IsNull(e.ServerCode);
            Assert.AreEqual(-1, e.NativeCode);
        }

        [Test]
        public void EmptyServerCodeIsNull()
        {
            Assert.IsNull(new AdError(AdErrorCode.NoFill, "x", "").ServerCode);
        }
    }
}

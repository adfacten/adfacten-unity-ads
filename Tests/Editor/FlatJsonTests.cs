using System.Collections.Generic;
using AdfactenAds.Internal;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    public class FlatJsonTests
    {
        [Test]
        public void ParsesScalars()
        {
            var map = FlatJson.Parse("{\"s\":\"a\\\"b\\\\c\\n\\u00e7\\u011f\", \"n\": -12.5e1, \"i\":42, \"t\":true, \"f\":false, \"z\":null}");
            Assert.AreEqual("a\"b\\c\nçğ", FlatJson.GetString(map, "s"));
            Assert.AreEqual(-125.0, FlatJson.GetDouble(map, "n"));
            Assert.AreEqual(42, FlatJson.GetInt(map, "i"));
            Assert.IsTrue(FlatJson.GetBool(map, "t"));
            Assert.IsFalse(FlatJson.GetBool(map, "f", true));
            Assert.IsNull(FlatJson.GetString(map, "z"));
            Assert.AreEqual(7, FlatJson.GetInt(map, "missing", 7));
        }

        [Test]
        public void SkipsNestedValuesAndKeepsTheRest()
        {
            var map = FlatJson.Parse("{\"a\":{\"x\":[1,{\"y\":\"}\"}]},\"b\":[\"]\"],\"c\":\"ok\"}");
            Assert.IsFalse(map.ContainsKey("a"));
            Assert.IsFalse(map.ContainsKey("b"));
            Assert.AreEqual("ok", FlatJson.GetString(map, "c"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"a\":")]
        [TestCase("{\"a\":\"unterminated")]
        public void BadInputNeverThrows(string json)
        {
            Assert.DoesNotThrow(() => FlatJson.Parse(json));
        }

        [Test]
        public void KeepsWhatWasReadBeforeAnError()
        {
            var map = FlatJson.Parse("{\"name\":\"no_fill\",\"message\":");
            Assert.AreEqual("no_fill", FlatJson.GetString(map, "name"));
        }

        [Test]
        public void WriteRoundTrips()
        {
            var json = FlatJson.Write(new[]
            {
                new KeyValuePair<string, object>("text", "quote \" slash \\ tab \t nl \n ctl \u0001 ü"),
                new KeyValuePair<string, object>("argb", 0xFF1E88E5L),
                new KeyValuePair<string, object>("max", 4294967295L),
                new KeyValuePair<string, object>("f", 12.5f),
                new KeyValuePair<string, object>("b", true),
                new KeyValuePair<string, object>("nil", null),
            });
            var map = FlatJson.Parse(json);
            Assert.AreEqual("quote \" slash \\ tab \t nl \n ctl \u0001 ü", FlatJson.GetString(map, "text"));
            Assert.AreEqual(0xFF1E88E5L, (long)FlatJson.GetDouble(map, "argb"));
            Assert.AreEqual(4294967295L, (long)FlatJson.GetDouble(map, "max"));
            Assert.AreEqual(12.5, FlatJson.GetDouble(map, "f"));
            Assert.IsTrue(FlatJson.GetBool(map, "b"));
            Assert.IsTrue(map.ContainsKey("nil"));
        }
    }
}

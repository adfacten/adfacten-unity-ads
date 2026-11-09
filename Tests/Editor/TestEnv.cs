using System;
using System.IO;
using AdfactenAds.Internal;
using NUnit.Framework;

namespace AdfactenAds.Tests
{
    /// <summary>A mock client on a hand-driven clock: nothing runs until <see cref="Advance"/>.</summary>
    internal sealed class TestEnv : IDisposable
    {
        private double _now;

        public TestEnv(bool initialize = true)
        {
            Queue = new MainThreadQueue(() => _now);
            Queue.OnError = e => { throw new AssertionException("queued action threw: " + e); };
            Mock = new MockClient(Queue) { PixelsPerDp = 1 };
            AdfactenRuntime.UseForTests(Mock, Queue);
            AdfactenMock.LoadError = null;
            AdfactenMock.LoadDelay = 0.5f;
            AdfactenMock.RewardedDuration = 5f;
            Logs = new System.Collections.Generic.List<string>();
            AdLog.Sink = (level, message) => Logs.Add(level + ": " + message);
            if (initialize) Adfacten.Initialize("apk_test", new AdfactenConfig { TestMode = true });
        }

        public MainThreadQueue Queue { get; private set; }
        public MockClient Mock { get; private set; }

        /// <summary>Plugin log lines as "level: message".</summary>
        public System.Collections.Generic.List<string> Logs { get; private set; }

        /// <summary>Moves the clock and drains until nothing else is due.</summary>
        public void Advance(double seconds)
        {
            _now += seconds;
            Drain();
        }

        public void Drain()
        {
            for (var i = 0; i < 100 && Queue.Drain() > 0; i++) { }
        }

        public void Dispose()
        {
            AdfactenMock.LoadError = null;
            AdLog.Sink = null;
            AdfactenRuntime.ResetStatics();
        }

        /// <summary>The package folder (Unity: Packages/com.adfacten.ads; dotnet: ADFACTEN_UNITY_PACKAGE), or null.</summary>
        public static string PackageRoot()
        {
            var env = Environment.GetEnvironmentVariable("ADFACTEN_UNITY_PACKAGE");
            foreach (var dir in new[] { env, "Packages/com.adfacten.ads" })
            {
                if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "package.json"))) return dir;
            }
            return null;
        }
    }
}

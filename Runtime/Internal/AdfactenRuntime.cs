using System.Threading;
using UnityEngine;

namespace AdfactenAds.Internal
{
    /// <summary>Shared plugin state: the platform client, the main-thread queue and the event router.</summary>
    internal static class AdfactenRuntime
    {
        private static IAdfactenClient _client;
        private static MainThreadQueue _queue;
        private static EventRouter _router;
        private static AdfactenDispatcher _dispatcher;
        private static bool _headless;
        private static int _nextHandle;

        public static IAdfactenClient Client
        {
            get
            {
                EnsureStarted();
                return _client;
            }
        }

        public static MainThreadQueue Queue
        {
            get
            {
                if (_queue == null) _queue = NewQueue();
                return _queue;
            }
        }

        public static EventRouter Router
        {
            get
            {
                if (_router == null) _router = new EventRouter();
                return _router;
            }
        }

        public static int NewHandle()
        {
            return Interlocked.Increment(ref _nextHandle);
        }

        /// <summary>Queues a bridge event for the main thread. Safe from any thread.</summary>
        public static void PostEvent(int handle, string name, string payloadJson)
        {
            var router = Router;
            Queue.Post(() => router.Dispatch(handle, name, payloadJson));
        }

        /// <summary>Starts the dispatcher (and the client) if needed. Call from the main thread.</summary>
        public static void EnsureStarted()
        {
            if (_client == null) _client = CreateClient();
            if (!_headless && _dispatcher == null) _dispatcher = AdfactenDispatcher.Create(Queue);
        }

        /// <summary>Tests: use <paramref name="client"/> and drain <paramref name="queue"/> by hand.</summary>
        internal static void UseForTests(IAdfactenClient client, MainThreadQueue queue)
        {
            ResetStatics();
            _headless = true;
            _client = client;
            _queue = queue;
        }

        /// <summary>Clears static state between play sessions (Enter Play Mode without domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            _client = null;
            _queue = null;
            _router = null;
            _dispatcher = null;
            _headless = false;
            _nextHandle = 0;
            AdfactenSettings.ResetCache();
        }

        private static MainThreadQueue NewQueue()
        {
            var q = new MainThreadQueue();
            q.OnError = AdLog.Exception;
            return q;
        }

        private static IAdfactenClient CreateClient()
        {
#if UNITY_EDITOR
            return CreateMock();
#elif UNITY_ANDROID
            return new AndroidClient();
#elif UNITY_IOS
            return new IosClient();
#else
            // Other platforms: placeholders in development builds, nothing in release builds.
            if (Debug.isDebugBuild) return CreateMock();
            return new UnavailableClient();
#endif
        }

        private static IAdfactenClient CreateMock()
        {
            var mock = new MockClient(Queue);
            if (!_headless) MockOverlay.Create(mock);
            return mock;
        }
    }
}

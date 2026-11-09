using UnityEngine;

namespace AdfactenAds.Internal
{
    /// <summary>Runs queued plugin callbacks on the Unity main thread, once per frame.</summary>
    [AddComponentMenu("")]
    internal sealed class AdfactenDispatcher : MonoBehaviour
    {
        private MainThreadQueue _queue;

        public static AdfactenDispatcher Create(MainThreadQueue queue)
        {
            var go = new GameObject("Adfacten");
            go.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
            var d = go.AddComponent<AdfactenDispatcher>();
            d._queue = queue;
            return d;
        }

        private void Update()
        {
            if (_queue != null) _queue.Drain();
        }
    }
}

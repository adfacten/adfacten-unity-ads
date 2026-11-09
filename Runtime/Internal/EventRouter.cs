using System.Collections.Generic;

namespace AdfactenAds.Internal
{
    /// <summary>Maps bridge handles to the C# objects that own them. Main thread only.</summary>
    internal sealed class EventRouter
    {
        private readonly Dictionary<int, IAdEventTarget> _targets = new Dictionary<int, IAdEventTarget>();

        public int Count { get { return _targets.Count; } }

        public void Register(int handle, IAdEventTarget target)
        {
            _targets[handle] = target;
        }

        public void Unregister(int handle)
        {
            _targets.Remove(handle);
        }

        public bool IsRegistered(int handle)
        {
            return _targets.ContainsKey(handle);
        }

        /// <summary>Delivers an event; events for unknown (destroyed) handles are dropped.</summary>
        public void Dispatch(int handle, string name, string payloadJson)
        {
            IAdEventTarget target;
            if (!_targets.TryGetValue(handle, out target)) return;
            target.HandleEvent(name, FlatJson.Parse(payloadJson));
        }

        public void Clear()
        {
            _targets.Clear();
        }
    }
}

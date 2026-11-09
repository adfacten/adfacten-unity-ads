using System;
using UnityEngine;

namespace AdfactenAds.Internal
{
    internal static class AdLog
    {
        private const string Prefix = "[Adfacten] ";

        /// <summary>Tests: receives (level, message) instead of the Unity console.</summary>
        internal static Action<string, string> Sink;

        public static void Info(string message)
        {
            if (Sink != null) { Sink("info", message); return; }
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            if (Sink != null) { Sink("warning", message); return; }
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            if (Sink != null) { Sink("error", message); return; }
            Debug.LogError(Prefix + message);
        }

        public static void Exception(Exception e)
        {
            if (Sink != null) { Sink("exception", e.ToString()); return; }
            Debug.LogException(e);
        }
    }
}

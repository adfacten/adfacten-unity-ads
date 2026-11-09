using System.Collections.Generic;
using AdfactenAds.Internal;

namespace AdfactenAds
{
    /// <summary>
    /// Why a load or show failed. Each value matches one stable error name shared by the Android and iOS SDKs
    /// (see <see cref="AdError.Name"/>).
    /// </summary>
    public enum AdErrorCode
    {
        /// <summary><c>internal_error</c>: unexpected SDK or plugin error, or an unknown name.</summary>
        InternalError = 0,
        /// <summary><c>not_initialized</c>: <see cref="Adfacten.Initialize(string, AdfactenConfig)"/> was not called, or the app key is malformed.</summary>
        NotInitialized,
        /// <summary><c>invalid_request</c>: for example an empty placement id.</summary>
        InvalidRequest,
        /// <summary><c>network_error</c>: network failure or timeout.</summary>
        NetworkError,
        /// <summary><c>no_fill</c>: no ad right now. The SDK waits at least 30 s before asking again for that placement.</summary>
        NoFill,
        /// <summary><c>server_error</c>: the ad server had an error (5xx).</summary>
        ServerError,
        /// <summary><c>invalid_response</c>: the ad server sent a response the SDK could not use.</summary>
        InvalidResponse,
        /// <summary><c>rejected</c>: the server refused the request. See <see cref="AdError.ServerCode"/>.</summary>
        Rejected,
        /// <summary><c>ad_expired</c>: the ad was loaded about 55 minutes ago or more. Load a new one.</summary>
        AdExpired,
        /// <summary><c>already_shown</c>: full-screen ads can be shown once.</summary>
        AlreadyShown,
        /// <summary><c>not_ready</c>: the ad is not loaded, or was already shown (iOS).</summary>
        NotReady,
        /// <summary><c>show_failed</c>: the ad could not be presented (another ad on screen, app in the background).</summary>
        ShowFailed,
        /// <summary><c>video_not_supported</c>: a video ad needs the Android video module (Project Settings &gt; Adfacten).</summary>
        VideoNotSupported,
        /// <summary><c>load_in_progress</c>: a load for this ad is already running.</summary>
        LoadInProgress,
        /// <summary><c>child_directed</c>: the app or request is child-directed; ads are not served.</summary>
        ChildDirected,
        /// <summary><c>creative_failed</c>: the creative failed to render (image download, video, HTML).</summary>
        CreativeFailed,
    }

    /// <summary>A load or show error.</summary>
    public sealed class AdError
    {
        private static readonly Dictionary<string, AdErrorCode> ByName = new Dictionary<string, AdErrorCode>
        {
            { "internal_error", AdErrorCode.InternalError },
            { "not_initialized", AdErrorCode.NotInitialized },
            { "invalid_request", AdErrorCode.InvalidRequest },
            { "network_error", AdErrorCode.NetworkError },
            { "no_fill", AdErrorCode.NoFill },
            { "server_error", AdErrorCode.ServerError },
            { "invalid_response", AdErrorCode.InvalidResponse },
            { "rejected", AdErrorCode.Rejected },
            { "ad_expired", AdErrorCode.AdExpired },
            { "already_shown", AdErrorCode.AlreadyShown },
            { "not_ready", AdErrorCode.NotReady },
            { "show_failed", AdErrorCode.ShowFailed },
            { "video_not_supported", AdErrorCode.VideoNotSupported },
            { "load_in_progress", AdErrorCode.LoadInProgress },
            { "child_directed", AdErrorCode.ChildDirected },
            { "creative_failed", AdErrorCode.CreativeFailed },
        };

        public AdError(AdErrorCode code, string message, string serverCode = null, int nativeCode = -1, string name = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            ServerCode = string.IsNullOrEmpty(serverCode) ? null : serverCode;
            NativeCode = nativeCode;
            Name = string.IsNullOrEmpty(name) ? NameOf(code) : name;
        }

        /// <summary>The error, parsed from <see cref="Name"/>.</summary>
        public AdErrorCode Code { get; private set; }

        /// <summary>
        /// The stable error name the native SDK reported (<c>no_fill</c>, <c>network_error</c>, <c>rejected</c>, …),
        /// the same on Android and iOS. Prefer it (or <see cref="Code"/>) in logs and analytics.
        /// </summary>
        public string Name { get; private set; }

        public string Message { get; private set; }

        /// <summary>
        /// The ad server's machine code when it refused the request: <c>unknown_app</c>, <c>app_not_approved</c>,
        /// <c>bundle_mismatch</c>, <c>platform_mismatch</c>, <c>placement_mismatch</c>, <c>coppa</c>,
        /// <c>test_only</c>, <c>invalid_request</c>. Treat it as an open set. Null when there is none.
        /// </summary>
        public string ServerCode { get; private set; }

        /// <summary>The platform SDK's numeric code (it differs between Android and iOS), or -1. For bug reports.</summary>
        public int NativeCode { get; private set; }

        /// <summary>The code for a stable error name; unknown names map to <see cref="AdErrorCode.InternalError"/>.</summary>
        public static AdErrorCode CodeOf(string name)
        {
            AdErrorCode code;
            return name != null && ByName.TryGetValue(name, out code) ? code : AdErrorCode.InternalError;
        }

        /// <summary>The stable error name of a code, e.g. <c>no_fill</c>.</summary>
        public static string NameOf(AdErrorCode code)
        {
            foreach (var kv in ByName)
            {
                if (kv.Value == code) return kv.Key;
            }
            return "internal_error";
        }

        /// <summary>Builds an error from a bridge payload: <c>name</c>, <c>message</c>, <c>serverCode</c>, <c>nativeCode</c>.</summary>
        internal static AdError FromPayload(Dictionary<string, object> map)
        {
            var name = FlatJson.GetString(map, "name");
            var message = FlatJson.GetString(map, "message");
            var serverCode = FlatJson.GetString(map, "serverCode");
            var nativeCode = FlatJson.GetInt(map, "nativeCode", -1);
            if (string.IsNullOrEmpty(name)) name = "internal_error";
            return new AdError(CodeOf(name), string.IsNullOrEmpty(message) ? name : message, serverCode, nativeCode, name);
        }

        public override string ToString()
        {
            return "AdError(" + Name + ": " + Message + (ServerCode != null ? ", serverCode: " + ServerCode : "") + ")";
        }
    }
}

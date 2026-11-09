namespace AdfactenAds
{
    /// <summary>
    /// Options for <see cref="Adfacten.Initialize(string, AdfactenConfig)"/>. Properties left null use the values
    /// from Project Settings &gt; Adfacten.
    /// </summary>
    public sealed class AdfactenConfig
    {
        /// <summary>
        /// Request test ads, which are never billed. Null (the default) means: on in development builds and in the
        /// Editor when "Test mode in development builds" is checked, off in release builds.
        /// </summary>
        public bool? TestMode { get; set; }

        /// <summary>Native SDK log level. Null uses the project setting (Warning by default).</summary>
        public LogLevel? LogLevel { get; set; }

        /// <summary>
        /// The app or this user is directed at children (COPPA / Google Families). The SDK then never reads the
        /// advertising ID and Adfacten does not serve ads. Null uses the project setting.
        /// </summary>
        public bool? ChildDirected { get; set; }

        /// <summary>
        /// Your own user id (up to 256 characters), sent with rewarded requests and echoed in the server-side reward
        /// callback. Can be changed later with <see cref="Adfacten.SetUserId"/>.
        /// </summary>
        public string UserId { get; set; }

        internal Resolved Resolve(AdfactenSettings settings, bool isDevelopmentBuild)
        {
            var r = new Resolved();
            r.TestMode = TestMode.HasValue ? TestMode.Value : (isDevelopmentBuild && settings.TestModeInDevelopmentBuilds);
            r.LogLevel = LogLevel.HasValue ? LogLevel.Value : settings.LogLevel;
            r.ChildDirected = ChildDirected.HasValue ? ChildDirected.Value : settings.ChildDirected;
            r.UserId = string.IsNullOrEmpty(UserId) ? null : UserId;
            return r;
        }

        internal struct Resolved
        {
            public bool TestMode;
            public LogLevel LogLevel;
            public bool ChildDirected;
            public string UserId;
        }
    }
}

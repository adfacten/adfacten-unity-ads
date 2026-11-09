namespace AdfactenAds
{
    /// <summary>
    /// Knobs for the Editor mock (no effect on Android and iOS devices). Use them to try your error and timing
    /// paths without a device, e.g. <c>AdfactenMock.LoadError = AdErrorCode.NoFill;</c>.
    /// </summary>
    public static class AdfactenMock
    {
        /// <summary>When set, every load fails with this error.</summary>
        public static AdErrorCode? LoadError;

        /// <summary>Seconds before a placeholder ad loads.</summary>
        public static float LoadDelay = 0.5f;

        /// <summary>Seconds a rewarded placeholder must stay open before the reward is granted.</summary>
        public static float RewardedDuration = 5f;
    }
}

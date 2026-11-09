namespace AdfactenAds
{
    /// <summary>Standard banner sizes in dp (Android) / points (iOS). Only these sizes are served.</summary>
    public sealed class AdSize
    {
        /// <summary>320x50.</summary>
        public static readonly AdSize Banner = new AdSize(320, 50, 0);

        /// <summary>320x100.</summary>
        public static readonly AdSize LargeBanner = new AdSize(320, 100, 1);

        /// <summary>300x250.</summary>
        public static readonly AdSize MediumRectangle = new AdSize(300, 250, 2);

        /// <summary>728x90, for tablets.</summary>
        public static readonly AdSize Leaderboard = new AdSize(728, 90, 3);

        private AdSize(int width, int height, int index)
        {
            Width = width;
            Height = height;
            Index = index;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>The value passed to the native bridges (0 banner, 1 large, 2 medium rectangle, 3 leaderboard).</summary>
        internal int Index { get; private set; }

        public override string ToString()
        {
            return Width + "x" + Height;
        }
    }
}

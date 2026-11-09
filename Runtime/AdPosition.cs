namespace AdfactenAds
{
    /// <summary>
    /// Where a banner sits on screen. Preset positions stay inside the safe area (notch, rounded corners).
    /// <see cref="Custom"/> takes Unity screen pixels measured from the top-left corner.
    /// </summary>
    public sealed class AdPosition
    {
        internal enum Kind
        {
            Top = 0,
            Bottom = 1,
            Center = 2,
            TopLeft = 3,
            TopRight = 4,
            BottomLeft = 5,
            BottomRight = 6,
            Custom = 7,
        }

        public static readonly AdPosition Top = new AdPosition(Kind.Top, 0, 0);
        public static readonly AdPosition Bottom = new AdPosition(Kind.Bottom, 0, 0);
        public static readonly AdPosition Center = new AdPosition(Kind.Center, 0, 0);
        public static readonly AdPosition TopLeft = new AdPosition(Kind.TopLeft, 0, 0);
        public static readonly AdPosition TopRight = new AdPosition(Kind.TopRight, 0, 0);
        public static readonly AdPosition BottomLeft = new AdPosition(Kind.BottomLeft, 0, 0);
        public static readonly AdPosition BottomRight = new AdPosition(Kind.BottomRight, 0, 0);

        private AdPosition(Kind kind, float x, float y)
        {
            Type = kind;
            X = x;
            Y = y;
        }

        /// <summary>
        /// The banner's top-left corner at (<paramref name="x"/>, <paramref name="y"/>) Unity screen pixels, measured
        /// from the top-left of the screen (Unity's <c>Screen</c> origin is bottom-left: use <c>Screen.height - y</c>).
        /// Not adjusted for the safe area.
        /// </summary>
        public static AdPosition Custom(float x, float y)
        {
            return new AdPosition(Kind.Custom, x, y);
        }

        internal Kind Type { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public bool IsCustom { get { return Type == Kind.Custom; } }

        public override string ToString()
        {
            return Type == Kind.Custom ? "Custom(" + X + ", " + Y + ")" : Type.ToString();
        }
    }
}

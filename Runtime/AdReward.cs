namespace AdfactenAds
{
    /// <summary>
    /// The reward configured on a rewarded placement, e.g. 10 <c>coins</c>. For UI only: credit the user from the
    /// signed server-side reward callback, never from the client.
    /// </summary>
    public sealed class AdReward
    {
        public AdReward(string type, double amount)
        {
            Type = type ?? string.Empty;
            Amount = amount;
        }

        public string Type { get; private set; }

        /// <summary>The amount set on the placement. The server always sends a whole number.</summary>
        public double Amount { get; private set; }

        public override string ToString()
        {
            return "AdReward(" + Amount + " " + Type + ")";
        }
    }
}

using System;
namespace PortableRules
{
    public static class Quote
    {
        // Configuration is the host's responsibility; the extracted rule has explicit inputs.
        public static decimal Total(decimal amount, decimal rate)
        {
            if (amount < 0 || amount > 1000000m) throw new ArgumentOutOfRangeException(nameof(amount));
            if (rate < 0 || rate > 1) throw new ArgumentOutOfRangeException(nameof(rate));
            return decimal.Round(checked(amount * (1 + rate)), 2, MidpointRounding.ToEven);
        }
    }
}

using System;
using System.Configuration;
using System.Globalization;

namespace LegacyRules
{
    // A legacy boundary tied to executable app.config, unrelated to an actual tax/financial rule.
    public static class LegacyQuote
    {
        public static decimal Total(decimal amount)
        {
            if (amount < 0 || amount > 1000000m) throw new ArgumentOutOfRangeException(nameof(amount));
            decimal rate = decimal.Parse(ConfigurationManager.AppSettings["feeRate"], CultureInfo.InvariantCulture);
            if (rate < 0 || rate > 1) throw new ArgumentOutOfRangeException(nameof(rate));
            return decimal.Round(checked(amount * (1 + rate)), 2, MidpointRounding.ToEven);
        }
    }
}

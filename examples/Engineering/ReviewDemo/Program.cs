using System.Globalization;
using Course.Review;

if (args is not [var amountText, var ageText, var category]
    || !decimal.TryParse(amountText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount)
    || !int.TryParse(ageText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int seconds)
    || seconds is < -86400 or > 86400)
{
    Console.Error.WriteLine("usage: amount age-seconds category");
    return 2;
}
try
{
    var policy = new ApprovalPolicy(new CategoryApprovalLimit(new Dictionary<string, decimal> { ["travel"] = 500m },
        new FixedApprovalLimit(1000m)), TimeSpan.FromMinutes(30));
    // Explicit clock value makes this demo and its process checks reproducible.
    var now = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    Console.WriteLine(policy.Evaluate(amount, category, now.AddSeconds(-seconds), now));
    return 0;
}
catch (ArgumentException) { Console.Error.WriteLine("usage: invalid amount or category"); return 2; }

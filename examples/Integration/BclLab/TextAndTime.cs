using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Course.BclLab;

public static partial class TextAndTime
{
    [GeneratedRegex("\\A[a-z][a-z0-9_-]{0,31}\\z", RegexOptions.CultureInvariant, 100)]
    public static partial Regex CategoryPattern();

    public static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo zone)
    {
        if (local.Kind != DateTimeKind.Unspecified) throw new ArgumentException("Use a local clock reading.");
        if (zone.IsInvalidTime(local)) throw new ArgumentException("Nonexistent local time.");
        if (zone.IsAmbiguousTime(local)) throw new ArgumentException("Ambiguous local time needs an offset.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    public static TimeZoneInfo TrainingZone()
    {
        var daylight = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
        var standard = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1), new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1), daylight, standard);
        return TimeZoneInfo.CreateCustomTimeZone("Training/Eastern", TimeSpan.FromHours(-5),
            "Training zone", "Standard", "Daylight", [rule]);
    }

    public static (int Utf16, int Scalars, int Elements) CountText(string text) =>
        (text.Length, text.EnumerateRunes().Count(), new StringInfo(text).LengthInTextElements);

    public static bool IsExpired(DateTimeOffset created, TimeSpan lifetime, TimeProvider clock) =>
        clock.GetUtcNow() >= created + lifetime;
}

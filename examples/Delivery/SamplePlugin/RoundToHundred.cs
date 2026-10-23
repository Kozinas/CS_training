using PluginContracts;
namespace SamplePlugin;
public sealed class RoundToHundred : IReportRule
{
    public string Name => "whole-hundreds";
    public long Apply(long amountMinor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountMinor);
        return amountMinor / 100 * 100;
    }
}

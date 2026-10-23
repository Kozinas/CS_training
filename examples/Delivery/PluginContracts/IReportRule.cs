namespace PluginContracts;
public interface IReportRule
{
    string Name { get; }
    long Apply(long amountMinor);
}

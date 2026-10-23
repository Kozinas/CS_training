using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;

namespace ClientCore;

public sealed record ReportRow(string Category, int Count, long AmountMinor);

public interface IReportSource
{
    Task<IReadOnlyList<ReportRow>> ReadAsync(string token, CancellationToken cancellationToken);
}

public sealed class DemoReportSource : IReportSource
{
    public async Task<IReadOnlyList<ReportRow>> ReadAsync(string token, CancellationToken cancellationToken)
    {
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        return [new("food", 2, 2000), new("travel", 1, 3000)];
    }
}

// The owner calls commands from its UI context and unsubscribes before disposal.
public sealed partial class ReportViewModel(IReportSource source) : ObservableObject, IDisposable
{
    [ObservableProperty] private string state = "Idle";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private IReadOnlyList<ReportRow> rows = Array.Empty<ReportRow>();
    public string Token { get; set; } = "";
    private bool disposed;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        State = "Loading";
        Error = "";
        Rows = Array.Empty<ReportRow>(); // Never present an old user's report as current.
        try
        {
            var result = await source.ReadAsync(Token, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed) return;
            Rows = result;
            State = result.Count == 0 ? "Empty" : "Ready";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!disposed) State = "Canceled";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TimeoutException)
        {
            if (disposed) return;
            State = "Error";
            Error = ex is TimeoutException ? "Request timed out." : "Cannot load report. Check access and connection.";
        }
    }

    public void Dispose()
    {
        disposed = true;
        LoadCommand.Cancel();
        Token = "";
    }
}

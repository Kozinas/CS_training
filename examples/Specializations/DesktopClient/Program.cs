using ClientCore;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace DesktopClient;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (args is ["--verify"])
        {
            var code = 1;
            app.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    using var vm = new ReportViewModel(new DemoReportSource());
                    var window = new MainWindow(vm); // Build XAML and bind without displaying a window.
                    await vm.LoadCommand.ExecuteAsync(null);
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    if (window.StateText.Text != "Ready" || window.ReportGrid.Items.Count != 2
                        || !window.LoadButton.IsEnabled) throw new InvalidOperationException("WPF binding failed.");
                    var pending = vm.LoadCommand.ExecuteAsync(null);
                    vm.LoadCancelCommand.Execute(null);
                    await pending;
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    if (window.StateText.Text != "Canceled" || window.ReportGrid.Items.Count != 0)
                        throw new InvalidOperationException("WPF cancellation failed.");
                    window.Close();
                    Console.WriteLine("PASS WPF: XAML, dispatcher, bindings, load and cancellation");
                    code = 0;
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); }
                finally { app.Shutdown(); }
            });
            app.Run();
            return code;
        }
        using HttpClient? http = Environment.GetEnvironmentVariable("LEDGER_API") is { } url
            ? LedgerHttpClient.CreateHttpClient(new Uri(url)) : null;
        IReportSource source = http is null ? new DemoReportSource() : new LedgerHttpClient(http);
        using var model = new ReportViewModel(source) { Token = Environment.GetEnvironmentVariable("LEDGER_TOKEN") ?? "" };
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        app.Run(new MainWindow(model));
        return 0;
    }
}

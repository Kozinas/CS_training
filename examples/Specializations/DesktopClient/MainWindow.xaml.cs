using ClientCore;
using System.Windows;

namespace DesktopClient;

public partial class MainWindow : Window
{
    public MainWindow(ReportViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Closed += (_, _) => model.Dispose();
    }
}

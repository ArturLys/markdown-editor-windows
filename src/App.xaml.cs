using System.Windows;

namespace MarkdownView;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow(e.Args.Length != 0 ? e.Args[0] : null).Show();
    }
}

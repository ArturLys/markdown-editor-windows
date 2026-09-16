using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MarkdownView;

public partial class App : Application
{
    public static string UserDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkdownView", "WebView2");

    /// Started before the window exists, so the WebView2 browser process spawns
    /// while WPF is still initialising rather than after it.
    public static Task<CoreWebView2Environment>? EnvTask { get; private set; }

    /// The file is read off-thread at the same time, for the same reason.
    public static Task<string>? FileTask { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Diag.Log("App.OnStartup");
        string? path = e.Args.Length != 0 ? e.Args[0] : null;

        // Trim what the browser process does on the way up: no component
        // updates, no first-run work, no background networking, and none of the
        // features this app can never use.
        var opts = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = string.Join(' ',
                "--disable-background-networking",
                "--disable-component-update",
                "--disable-sync",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-extensions",
                "--disable-renderer-backgrounding",
                "--disable-features=OptimizationHints,Translate,MediaRouter," +
                    "CalculateNativeWinOcclusion,AutofillServerCommunication"),
            EnableTrackingPrevention = false,
        };
        EnvTask = CoreWebView2Environment.CreateAsync(null, UserDataFolder, opts);
        Diag.Log("env create kicked off");

        if (path != null)
        {
            string full = Path.GetFullPath(path);
            FileTask = Task.Run(() => File.Exists(full) ? File.ReadAllText(full) : string.Empty);
        }

        base.OnStartup(e);
        new MainWindow(path).Show();
        Diag.Log("window shown (offscreen)");
    }
}

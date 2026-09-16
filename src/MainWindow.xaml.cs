using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkdownView;

public partial class MainWindow : Window
{
    // Where the window waits while the page loads, so an empty frame is never
    // shown. It is already screen-sized out there, so revealing it costs no
    // relayout.
    private const double Offscreen = -32000;

    private readonly string? _path;
    private bool _dirty;
    private bool _shown;
    private bool _closingForReal;

    public MainWindow(string? filePath)
    {
        Diag.Log("window ctor");
        InitializeComponent();
        Diag.Log("InitializeComponent done");

        filePath ??= PickFile();
        if (filePath == null)
        {
            Application.Current.Shutdown();
            return;
        }
        _path = Path.GetFullPath(filePath);
        Title = Path.GetFileName(_path);

        var area = SystemParameters.WorkArea;
        Width = area.Width;
        Height = area.Height;
        Left = Offscreen;
        Top = 0;

        PreviewKeyDown += OnPreviewKeyDown;
        CloseButton.Click += (_, _) => RequestClose();
        Closing += OnClosing;

        // Earliest point with a window handle, which is all WebView2 needs.
        // Waiting for Loaded would idle through WPF's first layout pass.
        SourceInitialized += async (_, _) =>
        {
            Diag.Log("SourceInitialized");
            try { await InitializeAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start editor:\n\n" + ex.Message, "MarkdownView",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _closingForReal = true;
                Close();
            }
        };

        // Never leave the window stranded off-screen if the page fails to report in.
        var failsafe = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        failsafe.Tick += (_, _) => { failsafe.Stop(); Reveal(); };
        failsafe.Start();
    }

    /// Bring the finished window on screen, once.
    private void Reveal()
    {
        if (_shown) return;
        _shown = true;
        Left = 0;
        Top = 0;
        WindowState = WindowState.Maximized;
        Activate();
        ClosePopup.HorizontalOffset = ActualWidth - CloseButton.Width;
        ClosePopup.VerticalOffset = 0;
        WebView.Focus();
        Diag.Log("window revealed");
        Diag.Flush();
    }

    private static string? PickFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open Markdown File",
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private async Task InitializeAsync()
    {
        // Kicked off in App.OnStartup, so this has usually finished already.
        var env = App.EnvTask is not null
            ? await App.EnvTask
            : await CoreWebView2Environment.CreateAsync(null, App.UserDataFolder);
        Diag.Log("env ready");

        await WebView.EnsureCoreWebView2Async(env);
        Diag.Log("EnsureCoreWebView2 done");

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.WebMessageReceived += OnWebMessage;

        // The host name must not end in .local: that suffix is reserved for
        // multicast DNS, so Windows spends ~2s asking the network about it
        // before the request ever reaches this folder mapping.
        string webDir = Path.Combine(AppContext.BaseDirectory, "web");
        core.SetVirtualHostNameToFolderMapping("appassets.example", webDir,
            CoreWebView2HostResourceAccessKind.Allow);
        core.Navigate("https://appassets.example/index.html");
        Diag.Log("navigate called");
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        string type = doc.RootElement.GetProperty("type").GetString() ?? "";
        switch (type)
        {
            case "ready":
                Diag.Log("page ready");
                string md = App.FileTask is not null
                    ? await App.FileTask
                    : (_path != null && File.Exists(_path) ? File.ReadAllText(_path) : string.Empty);
                await WebView.ExecuteScriptAsync(
                    "window.loadMarkdown(" + JsonSerializer.Serialize(md) + ")");
                _dirty = false;
                Diag.Log("content handed to page");
                break;
            case "painted":
                Diag.Log("page painted");
                Reveal();
                break;
            case "dirty":
                _dirty = true;
                break;
            case "save":
                Save(doc.RootElement.GetProperty("md").GetString() ?? "");
                break;
            case "close":
                if (_dirty) Save(doc.RootElement.GetProperty("md").GetString() ?? "");
                _closingForReal = true;
                Close();
                break;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        if (ctrl && e.Key == Key.S) { _ = SaveFromPageAsync(); e.Handled = true; }
        else if (e.Key == Key.Escape || (e.Key == Key.F4 && alt)) { RequestClose(); e.Handled = true; }
    }

    private async Task<string?> PullMarkdownAsync()
    {
        if (WebView.CoreWebView2 is null) return null;
        try
        {
            string json = await WebView.ExecuteScriptAsync("window.getMarkdown()");
            return JsonSerializer.Deserialize<string>(json);
        }
        catch { return null; }
    }

    private async Task SaveFromPageAsync()
    {
        string? md = await PullMarkdownAsync();
        if (md != null) Save(md);
    }

    private void Save(string md)
    {
        if (_path == null) return;
        try
        {
            File.WriteAllText(_path, md);
            _dirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not save:\n\n" + ex.Message, "MarkdownView",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RequestClose()
    {
        if (_dirty) await SaveFromPageAsync();
        _closingForReal = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closingForReal || !_dirty) return;
        e.Cancel = true;
        RequestClose();
    }
}

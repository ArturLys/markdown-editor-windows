using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkdownView;

public partial class MainWindow : Window
{
    private readonly string? _path;
    private bool _dirty;
    private bool _webReady;
    private bool _closingForReal;

    public MainWindow(string? filePath)
    {
        InitializeComponent();

        filePath ??= PickFile();
        if (filePath == null)
        {
            Application.Current.Shutdown();
            return;
        }
        _path = Path.GetFullPath(filePath);
        Title = Path.GetFileName(_path);

        PreviewKeyDown += OnPreviewKeyDown;
        CloseButton.Click += (_, _) => RequestClose();
        Closing += OnClosing;

        Loaded += async (_, _) =>
        {
            ClosePopup.HorizontalOffset = ActualWidth - CloseButton.Width;
            ClosePopup.VerticalOffset = 0;
            try { await InitializeAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start editor:\n\n" + ex.Message, "MarkdownView", MessageBoxButton.OK, MessageBoxImage.Error);
                _closingForReal = true;
                Close();
            }
        };
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
        string userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownView", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.WebMessageReceived += OnWebMessage;

        string webDir = Path.Combine(AppContext.BaseDirectory, "web");
        core.SetVirtualHostNameToFolderMapping("app.local", webDir, CoreWebView2HostResourceAccessKind.Allow);
        core.Navigate("https://app.local/index.html");
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        string type = doc.RootElement.GetProperty("type").GetString() ?? "";
        switch (type)
        {
            case "ready":
                _webReady = true;
                string md = _path != null && File.Exists(_path) ? File.ReadAllText(_path) : string.Empty;
                await WebView.ExecuteScriptAsync("window.loadMarkdown(" + JsonSerializer.Serialize(md) + ")");
                _dirty = false;
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
        if (!_webReady) return null;
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
            MessageBox.Show("Could not save:\n\n" + ex.Message, "MarkdownView", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // Something external (Alt+F4 via system menu, taskbar) is closing us: save first, then really close.
        e.Cancel = true;
        RequestClose();
    }
}

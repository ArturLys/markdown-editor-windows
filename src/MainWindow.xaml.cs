using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkdownView;

public partial class MainWindow : Window
{
    // The window loads out here and slides into place once the document has
    // painted, so an empty frame is never on screen. Two neater approaches were
    // tried first and both failed: a layered window (WPF reverts extended
    // styles it did not set itself) and simply not showing the window (an
    // unshown WPF window never lays out, so nothing inside it initialises).
    private const double Offscreen = -32000;

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Pt { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rct { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rct rcMonitor;
        public Rct rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Pt p);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Pt pt, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfo mi);

    private readonly string? _path;
    private Rect _target;
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

        // A handle is needed to convert screen pixels into the units Left and
        // Top use. Creating it here does not put the window on screen.
        new WindowInteropHelper(this).EnsureHandle();

        _target = TargetScreen();
        Width = _target.Width;
        Height = _target.Height;
        Top = _target.Y;
        Left = Offscreen;
        Diag.Log($"target screen {_target.Width}x{_target.Height} at {_target.X},{_target.Y}");

        PreviewKeyDown += OnPreviewKeyDown;
        CloseButton.Click += (_, _) => RequestClose();
        Closing += OnClosing;
        LocationChanged += (_, _) => PositionCloseButton();
        SizeChanged += (_, _) => PositionCloseButton();

        Loaded += async (_, _) =>
        {
            Diag.Log("window Loaded");
            try { await InitializeAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to start editor:\n\n" + ex.Message, "MarkdownView",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _closingForReal = true;
                Close();
            }
        };

        // Never leave the window stranded off-screen if the page never reports in.
        var failsafe = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        failsafe.Tick += (_, _) => { failsafe.Stop(); Reveal(); };
        failsafe.Start();
    }

    /// The full bounds of the screen the pointer is on, in window units. Going
    /// by the pointer means the document opens on the monitor being worked on,
    /// and taking full bounds rather than the working area means it covers the
    /// screen the way a maximised borderless window does.
    /// MDV_POS="x,y" picks the monitor containing that screen pixel instead.
    private Rect TargetScreen()
    {
        Pt probe;
        string? forced = Environment.GetEnvironmentVariable("MDV_POS");
        string[] parts = forced?.Split(',') ?? Array.Empty<string>();
        if (parts.Length == 2 && int.TryParse(parts[0], out int fx) && int.TryParse(parts[1], out int fy))
            probe = new Pt { X = fx, Y = fy };
        else if (!GetCursorPos(out probe))
            probe = new Pt { X = 0, Y = 0 };

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        IntPtr mon = MonitorFromPoint(probe, MonitorDefaultToNearest);
        if (mon == IntPtr.Zero || !GetMonitorInfoW(mon, ref info))
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth,
                                  SystemParameters.PrimaryScreenHeight);

        var r = info.rcMonitor;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null)
            return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        var toDip = src.CompositionTarget.TransformFromDevice;
        Point origin = toDip.Transform(new Point(r.Left, r.Top));
        Point corner = toDip.Transform(new Point(r.Right, r.Bottom));
        return new Rect(origin, corner);
    }

    /// Slide the finished window into place. A move only: no resize and no
    /// window-state change, either of which makes Windows repaint the frame
    /// before the web view draws into it, which reads as a white flash.
    private void Reveal()
    {
        if (_shown) return;
        _shown = true;
        Left = _target.X;
        Top = _target.Y;
        Activate();
        PositionCloseButton();
        ClosePopup.IsOpen = true;
        WebView.Focus();
        Diag.Log($"window revealed at {ActualWidth}x{ActualHeight}");
        Diag.Flush();
    }

    /// Park the close button in this window's own top-right corner. Popup
    /// offsets are absolute screen coordinates, so a window on a second monitor
    /// needs the window's real position, not an assumed origin of zero.
    private void PositionCloseButton()
    {
        if (!_shown || PresentationSource.FromVisual(this) is null) return;
        ClosePopup.HorizontalOffset = Left + ActualWidth - CloseButton.Width;
        ClosePopup.VerticalOffset = Top;
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

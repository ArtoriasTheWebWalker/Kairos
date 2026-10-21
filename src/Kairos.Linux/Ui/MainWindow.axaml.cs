using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kairos.App;
using Kairos.Ui.Pages;

namespace Kairos.Ui;

public partial class MainWindow : Window
{
    readonly SettingsViewModel _vm;
    readonly ClipsViewModel _clips;
    ClipsPage? _clipsPage;
    readonly Dictionary<string, Func<Control>> _pages;
    string? _current;

    public MainWindow() => throw new NotSupportedException("The settings window needs its view model.");

    public MainWindow(SettingsViewModel vm)
    {
        _vm = vm;
        _clips = new ClipsViewModel(() => _vm.EffectiveFolder, () => _vm.HkSave, () => _vm.HkScreenshot);
        InitializeComponent();
        DataContext = vm;
        LibraryBar.DataContext = _clips; // the Library's chosen clips
        // The share panel is not ported yet: Share shows the file in the file manager.
        _clips.ShareRequested += clip => ShowInFolder(clip.Path);
        _vm.ShareRequested += path => ShowInFolder(path);
        Title = Text.Get("app");
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://kairos/Assets/icons/kairos-256.png")));
        Wordmark.Source = new Bitmap(AssetLoader.Open(new Uri($"avares://kairos/Ui/Assets/wordmark-{(Text.IsArabic ? "ar" : "en")}.png")));
        Wordmark.Height = Text.IsArabic ? 28 : 22;
        FlowDirection = Text.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;

        _pages = new()
        {
            ["status"] = () => new StatusPage(),
            ["clips"] = () => { _clips.Refresh(); return _clipsPage ??= new ClipsPage { DataContext = _clips }; },
            ["capture"] = () => new CapturePage(),
            ["quality"] = () => new QualityPage(),
            ["audio"] = () => new AudioPage(),
            ["hotkeys"] = () => new HotkeysPage(),
            ["voice"] = () => new SmartSavePage(),
            ["general"] = () => new GeneralPage(),
        };
        foreach (var (button, page) in new (RadioButton, string)[]
                 {
                     (NavStatus, "status"), (NavClips, "clips"), (NavCapture, "capture"), (NavQuality, "quality"),
                     (NavAudio, "audio"), (NavHotkeys, "hotkeys"), (NavVoice, "voice"), (NavConnections, "connections"), (NavGeneral, "general"),
                 })
        {
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true)
                    ShowPage(page);
            };
            Avalonia.Automation.AutomationProperties.SetName(button, button.Content as string ?? page);
        }
        // Screenshots in the window's own viewer, videos in its own player
        _clips.PictureRequested += ShowPicture;
        _vm.PictureRequested += path => ShowPicture(PictureItem(path));
        PictureLayer.ShareRequested += clip => ShowInFolder(clip.Path);
        PictureLayer.ShowRequested += clip => _clips.ShowCommand.Execute(clip);
        PictureLayer.DeleteRequested += clip => _clips.DeleteCommand.Execute(clip);
        PictureLayer.Neighbor = _clips.NextPicture;
        _clips.VideoRequested += clip =>
        {
            Engine.ObsLog.Info($"video requested: {clip.Path}");
            try { VideoLayer.Show(clip); Engine.ObsLog.Info("viewer shown"); }
            catch (Exception e) { Engine.ObsLog.Error("viewer: " + e); }
        };
        VideoLayer.ShareRequested += clip => ShowInFolder(clip.Path);
        VideoLayer.ShowRequested += clip => _clips.ShowCommand.Execute(clip);
        VideoLayer.DeleteRequested += clip => _clips.DeleteCommand.Execute(clip);
        Closed += (_, _) => VideoLayer.Hide();
        // A clip or screenshot saved while the Library is open shows at once, marked new.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.LastClipPath) or null)
                OnLastClip(_vm.LastClipPath);
            if (e.PropertyName is nameof(SettingsViewModel.LastShotPath) or null)
                OnLastShot(_vm.LastShotPath);
        };
        vm.NavigateRequested += ShowPage;
        // WPF asks every command whether it can run again after each bit of input; Avalonia only when told.
        vm.PropertyChanged += (_, _) => RelayCommand.RequeryLater();
        AddHandler(PointerReleasedEvent, (_, _) => RelayCommand.RequeryLater(), handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, _) => RelayCommand.RequeryLater(), handledEventsToo: true);
        ShowPage("status");

        // Chrome: drag by the top strip, resize by the edges, double-click to maximize.
        DragBar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
                ToggleMaximize();
            else
                BeginMoveDrag(e);
        };
        void Edge(Border edge, WindowEdge which) => edge.PointerPressed += (_, e) =>
        {
            if (WindowState == WindowState.Normal && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginResizeDrag(which, e);
        };
        Edge(EdgeLeft, WindowEdge.West);
        Edge(EdgeRight, WindowEdge.East);
        Edge(EdgeTop, WindowEdge.North);
        Edge(EdgeBottom, WindowEdge.South);
        MinButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxButton.Click += (_, _) => ToggleMaximize();
        CloseButton.Click += (_, _) => Close();
    }

    static void ShowInFolder(string path)
    {
        if (File.Exists(path))
            System.Diagnostics.Process.Start(ShellPaths.ShowInFolder(path));
    }

    /// <summary>Shows the screenshot in the window's own viewer.</summary>
    public void ShowPicture(ClipItem picture) => PictureLayer.Show(picture);

    /// <summary>The Library's card for a picture, or one made for it when the page has not listed it yet.</summary>
    ClipItem PictureItem(string path)
    {
        var known = _clips.Items.FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.Ordinal));
        if (known != null)
            return known;
        var file = new FileInfo(path);
        return new ClipItem
        {
            Path = path, Name = Path.GetFileNameWithoutExtension(path), App = Engine.ClipLibrary.GroupOf(_vm.EffectiveFolder, path),
            Time = file.Exists ? file.LastWriteTime : DateTime.Now, Size = file.Exists ? file.Length : 0, Kind = Engine.ClipKind.Picture,
        };
    }

    string? _lastClip, _lastShot;

    /// <summary>A clip or recording was saved while the Library is open: only what changed on disk is added.</summary>
    void OnLastClip(string? path)
    {
        if (string.Equals(path, _lastClip, StringComparison.Ordinal))
            return;
        bool first = _lastClip == null;
        _lastClip = path;
        if (!first && _current == "clips")
            _clips.Refresh(live: true);
    }

    /// <summary>A screenshot taken while the Library is open shows at once, like a clip.</summary>
    void OnLastShot(string? path)
    {
        if (string.Equals(path, _lastShot, StringComparison.Ordinal))
            return;
        _lastShot = path;
        if (path != null && _current == "clips")
            _clips.Refresh(live: true);
    }

    void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void ShowPage(string name)
    {
        if (_current == name || !_pages.TryGetValue(name, out var make))
            return;
        _current = name;
        // The Clips page stays in its own host (hidden on other pages): coming back needs no rebuilding or new layout.
        bool clips = name == "clips";
        ClipsHost.IsVisible = clips;
        PageHost.IsVisible = !clips;
        LibraryBarLayer.IsVisible = clips;
        if (clips)
            ClipsHost.Content = make();
        else
            PageHost.Content = make();
        Scroller.ScrollToHome();
        var nav = name switch
        {
            "status" => NavStatus, "clips" => NavClips, "capture" => NavCapture, "quality" => NavQuality, "audio" => NavAudio,
            "hotkeys" => NavHotkeys, "voice" => NavVoice, "connections" => NavConnections, _ => NavGeneral,
        };
        if (nav.IsChecked != true)
            nav.IsChecked = true;
    }
}

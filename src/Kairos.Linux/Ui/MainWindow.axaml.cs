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
    readonly Dictionary<string, Func<Control>> _pages;
    string? _current;

    public MainWindow() => throw new NotSupportedException("The settings window needs its view model.");

    public MainWindow(SettingsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        Title = Text.Get("app");
        Icon = new WindowIcon(new MemoryStream(AppIconArt.Png(AppIconArt.Draw(256, Settings.ThemeColors.Accent))));
        Wordmark.Source = new Bitmap(AssetLoader.Open(new Uri($"avares://kairos/Ui/Assets/wordmark-{(Text.IsArabic ? "ar" : "en")}.png")));
        Wordmark.Height = Text.IsArabic ? 28 : 22;
        FlowDirection = Text.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;

        _pages = new()
        {
            ["status"] = () => new StatusPage(),
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

    void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void ShowPage(string name)
    {
        if (_current == name || !_pages.TryGetValue(name, out var make))
            return;
        _current = name;
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

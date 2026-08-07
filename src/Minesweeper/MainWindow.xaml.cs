using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Minesweeper.Core;
using Minesweeper.Controls;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace Minesweeper;

public sealed partial class MainWindow : Window
{
    private readonly SettingsData _settings;
    private GameEngine _engine = null!;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _elapsedSeconds;

    // 主题菜单项（代码构建，因为 MenuFlyout 在本机 XamlCompiler 有崩溃问题）
    private readonly RadioMenuFlyoutItem _themeLightItem = new() { Text = "浅色", Tag = "Light", GroupName = "ThemeGroup" };
    private readonly RadioMenuFlyoutItem _themeDarkItem = new() { Text = "深色", Tag = "Dark", GroupName = "ThemeGroup" };
    private readonly RadioMenuFlyoutItem _themeSystemItem = new() { Text = "跟随系统", Tag = "System", GroupName = "ThemeGroup" };

    public MainWindow()
    {
        App.Log("MainWindow: InitializeComponent");
        InitializeComponent();
        App.Log("MainWindow: 初始化完成");
        Title = "扫雷";

        // 标题栏扩展：内容延伸到标题栏，Mica 贯穿标题栏与工具栏
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);

        // 亚克力背景（通透毛玻璃，比 Mica 更透；色调随系统主题自动）
        App.Log("MainWindow: 设置亚克力背景");
        SystemBackdrop = new DesktopAcrylicBackdrop();
        App.Log("MainWindow: 背景完成");

        App.Log("MainWindow: 加载设置");
        _settings = SettingsStore.Load();
        Board.SetKeyBindings(_settings.KeyBindings);
        App.Log("MainWindow: 构建菜单");
        BuildMenus();

        _timer.Tick += (_, _) => UpdateTimer();

        // 按键统一在窗口根元素路由：无论焦点在哪个控件（含无焦点状态）都能响应键盘操作
        RootGrid.KeyDown += OnRootKeyDown;

        // 焦点管理：WinUI 3 中按键只派发给焦点元素所在分支，必须保证窗口内有元素持焦。
        // 启动时多次尝试聚焦棋盘（Loaded 时窗口可能尚未就绪，聚焦会静默失败），
        // 窗口重新激活时也归还焦点，保证键盘随时可用。
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                FocusBoard();
        };

        // 主题与窗口尺寸在加载后应用（此时 XamlRoot 可用、布局完成）
        App.Log("MainWindow: 注册加载后处理");
        RootGrid.Loaded += (_, _) =>
        {
            App.Log("MainWindow: 窗口加载完成，应用主题与尺寸");
            ApplyTheme(_settings.Theme);
            UpdateThemeMenuState();
            SizeWindowToBoard();
            FocusBoard();
            // 双保险：等窗口完全就绪后再聚焦一次（Loaded 时聚焦可能被窗口激活流程吞掉）
            DispatcherQueue.TryEnqueue(() => FocusBoard());
        };

        Board.GameOver += OnGameOver;
        Board.FirstMove += OnFirstMove;
        Board.StateChanged += OnStateChanged;
        Board.HintRequested += () => OnHintClick(null, null);
        Board.RestartRequested += () => OnNewGameClick(null, null);
        Board.UndoRequested += () => OnUndoClick(null, null);

        // 撤销按钮快捷键提示（跟随用户自定义键位）
        ToolTipService.SetToolTip(UndoButton,
            $"快捷键 {BoundKeys(GameAction.Undo)}：撤销上一步操作（可后悔踩雷）");

        App.Log("MainWindow: 开始游戏");
        StartNewGame(_settings.Spec);
        App.Log("MainWindow: 构造完成");
    }

    /// <summary>代码构建菜单（MenuFlyout 元素在 XAML 中会导致本机 XamlCompiler 崩溃）</summary>
    private void BuildMenus()
    {
        // 圆角菜单样式
        var roundedStyle = (Style)Application.Current.Resources["RoundedMenuFlyoutPresenterStyle"];

        // 难度菜单
        var difficulty = new MenuFlyout
        {
            MenuFlyoutPresenterStyle = roundedStyle,
        };
        var beginner = new MenuFlyoutItem { Text = "初级（9×9 · 10 雷）", Tag = "Beginner" };
        var intermediate = new MenuFlyoutItem { Text = "中级（16×16 · 40 雷）", Tag = "Intermediate" };
        var expert = new MenuFlyoutItem { Text = "高级（30×16 · 99 雷）", Tag = "Expert" };
        var custom = new MenuFlyoutItem { Text = "自定义…" };
        beginner.Click += OnDifficultyClick;
        intermediate.Click += OnDifficultyClick;
        expert.Click += OnDifficultyClick;
        custom.Click += OnCustomClick;
        difficulty.Items.Add(beginner);
        difficulty.Items.Add(intermediate);
        difficulty.Items.Add(expert);
        difficulty.Items.Add(new MenuFlyoutSeparator());
        difficulty.Items.Add(custom);
        DifficultyButton.Flyout = difficulty;

        // 主题菜单
        _themeLightItem.Click += OnThemeClick;
        _themeDarkItem.Click += OnThemeClick;
        _themeSystemItem.Click += OnThemeClick;
        var theme = new MenuFlyout
        {
            MenuFlyoutPresenterStyle = roundedStyle,
        };
        theme.Items.Add(_themeLightItem);
        theme.Items.Add(_themeDarkItem);
        theme.Items.Add(_themeSystemItem);
        ThemeButton.Flyout = theme;

        // 帮助菜单
        var help = new MenuFlyout
        {
            MenuFlyoutPresenterStyle = roundedStyle,
        };
        var helpItem = new MenuFlyoutItem { Text = "操作与快捷键说明…" };
        helpItem.Click += OnHelpClick;
        help.Items.Add(helpItem);
        var keySettingsItem = new MenuFlyoutItem { Text = "键位设置…" };
        keySettingsItem.Click += OnKeySettingsClick;
        help.Items.Add(keySettingsItem);
        HelpButton.Flyout = help;
    }

    /// <summary>
    /// 窗口根元素按键路由：棋盘按键处理上移到这里，
    /// 无论焦点在棋盘、工具栏还是无焦点状态，键盘操作都有效。
    /// （打开中的菜单/对话框是独立输入范围，按键不会传到这里）
    /// </summary>
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // WinUI 3 的 KeyRoutedEventArgs 无修饰键属性，从输入状态查询
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        bool alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(CoreVirtualKeyStates.Down);
        bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);

        if (Board.HandleKey(e.Key, ctrl, alt, shift))
            e.Handled = true; // 已消费：阻止 XY 焦点导航等默认行为
    }

    /// <summary>把键盘焦点放回棋盘（按键路由要求窗口内至少一个元素持焦）</summary>
    private void FocusBoard()
    {
        bool ok = Board.Focus(FocusState.Programmatic);
        App.Log($"FocusBoard: {(ok ? "成功" : "失败")}");
    }

    // ---------- 游戏生命周期 ----------

    private void StartNewGame(DifficultySpec spec)
    {
        _timer.Stop();
        _elapsedSeconds = 0;
        _engine = new GameEngine(spec);
        Board.StartNewGame(_engine);
        UpdateHud();
        UpdateUndoState();
        StatusBar.IsOpen = false;
        // 窗口尺寸在 RootGrid.Loaded 后设置（XamlRoot 可用时）
    }

    private void OnNewGameClick(object sender, RoutedEventArgs e)
    {
        StartNewGame(_settings.Spec);
        Board.Focus(FocusState.Programmatic); // 归还焦点，键盘操作立即可用
    }

    private void OnFaceButtonClick(object sender, RoutedEventArgs e)
    {
        StartNewGame(_settings.Spec);
        Board.Focus(FocusState.Programmatic);
    }

    // ---------- 撤销 ----------

    /// <summary>撤销上一步操作（含"后悔踩雷"：游戏结束后仍可撤销回到结束前）</summary>
    private void OnUndoClick(object? sender, RoutedEventArgs? e)
    {
        if (!_engine.Undo()) return;

        // 撤销回到开局前：停止并重置计时
        if (_engine.Phase == GamePhase.Ready)
        {
            _timer.Stop();
            _elapsedSeconds = 0;
            TimerText.Text = "000";
        }
        // 关闭胜负提示条，表情复位（UpdateHud 处理）
        StatusBar.IsOpen = false;
        Board.RefreshAll();
        UpdateHud();
        UpdateUndoState();
        Board.Focus(FocusState.Programmatic);
    }

    /// <summary>撤销按钮可用状态：撤销栈非空才可点</summary>
    private void UpdateUndoState() => UndoButton.IsEnabled = _engine.CanUndo;

    private void OnFirstMove()
    {
        _elapsedSeconds = 0;
        TimerText.Text = "000";
        _timer.Start();
    }

    private void OnStateChanged()
    {
        UpdateHud();
        UpdateUndoState();
    }

    private void OnGameOver()
    {
        _timer.Stop();
        UpdateHud();

        if (_engine.Phase == GamePhase.Won)
        {
            FaceText.Text = "😎";
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = "胜利！";
            StatusBar.Message = $"全部地雷已标出，用时 {_elapsedSeconds} 秒。";
            StatusBar.IsOpen = true;
        }
        else if (_engine.Phase == GamePhase.Lost)
        {
            FaceText.Text = "😵";
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Title = "踩到地雷了！";
            StatusBar.Message = "点击表情按钮或按 R 重新开始。";
            StatusBar.IsOpen = true;
        }
    }

    private void UpdateHud()
    {
        MinesRemainingText.Text = Math.Clamp(_engine.RemainingMines, -99, 999).ToString("000");
        if (_engine.Phase != GamePhase.Won && _engine.Phase != GamePhase.Lost)
            FaceText.Text = "😀";
    }

    private void UpdateTimer()
    {
        _elapsedSeconds = Math.Min(_elapsedSeconds + 1, 999);
        TimerText.Text = _elapsedSeconds.ToString("000");
    }

    // ---------- 难度 ----------

    private void OnDifficultyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        _settings.Difficulty = item.Tag switch
        {
            "Beginner" => GameDifficulty.Beginner,
            "Intermediate" => GameDifficulty.Intermediate,
            "Expert" => GameDifficulty.Expert,
            _ => GameDifficulty.Beginner,
        };
        SettingsStore.Save(_settings);
        StartNewGame(_settings.Spec);
        Board.Focus(FocusState.Programmatic); // 归还焦点
    }

    private async void OnCustomClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomGameDialog { XamlRoot = Content.XamlRoot };
        dialog.Prefill(_settings);
        var result = await dialog.ShowAsync();
        Board.Focus(FocusState.Programmatic);
        if (result != ContentDialogResult.Primary) return;

        var spec = dialog.GetSpec();
        _settings.SetCustom(spec.Width, spec.Height, spec.Mines);
        SettingsStore.Save(_settings);
        StartNewGame(_settings.Spec);
    }

    // ---------- 提示 ----------

    private void OnHintClick(object? sender, RoutedEventArgs? e)
    {
        if (_engine.IsOver)
        {
            ShowStatus(InfoBarSeverity.Informational, "游戏已结束", "请重新开始后再使用提示。");
            return;
        }
        if (!Board.ApplyHint())
        {
            ShowStatus(InfoBarSeverity.Warning, "没有可提示的安全格", "棋盘上的安全格已全部翻开或标满旗子。");
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    // ---------- 主题 ----------

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem item) return;
        _settings.Theme = item.Tag switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.System,
        };
        SettingsStore.Save(_settings);
        ApplyTheme(_settings.Theme);
        UpdateThemeMenuState();
        Board.Focus(FocusState.Programmatic); // 归还焦点
    }

    private UISettings? _uiSettings;

    private void ApplyTheme(AppTheme theme)
    {
        if (_uiSettings != null)
        {
            _uiSettings.ColorValuesChanged -= OnSystemThemeChanged;
            _uiSettings = null;
        }

        // 本机 WinUI 运行时 Application.RequestedTheme 设置受限（0x80131515），
        // 改用根元素级 ElementTheme（等效的明暗切换）
        RootGrid.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default, // 跟随系统
        };

        // 标题栏三个按钮（最小化/最大化/关闭）由 DWM 按系统主题绘制，
        // 与应用主题不一致时会出现浅色窗口上白色按钮不可见的问题，这里按应用主题显式取色
        UpdateCaptionButtonColors();

        Board.RefreshAll();
    }

    /// <summary>
    /// 窗口标题栏按钮取色：浅色主题用深色图标、深色主题用白色图标，
    /// 悬停/按下背景用半透明叠加色（Win11 风格）。
    /// </summary>
    private void UpdateCaptionButtonColors()
    {
        try
        {
            var tb = AppWindow.TitleBar;
            bool dark = _settings.Theme switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => IsSystemDark(),
            };
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonForegroundColor = dark ? Colors.White : Color.FromArgb(0xAF, 0x00, 0x00, 0x00);
            tb.ButtonInactiveForegroundColor = dark
                ? Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x52, 0x00, 0x00, 0x00);
            tb.ButtonHoverBackgroundColor = dark
                ? Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0x00, 0x00, 0x00);
            tb.ButtonPressedBackgroundColor = dark
                ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x26, 0x00, 0x00, 0x00);
        }
        catch
        {
            // 个别环境标题栏 API 不可用时保持系统默认
        }
    }

    /// <summary>系统是否处于深色模式（按背景色亮度估算）</summary>
    private bool IsSystemDark()
    {
        _uiSettings ??= new UISettings();
        var color = _uiSettings.GetColorValue(UIColorType.Background);
        double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
        return brightness < 0.5;
    }

    private void ApplySystemTheme()
    {
        // 根元素 ElementTheme.Default 会跟随系统；此处仅刷新标题栏按钮取色
        UpdateCaptionButtonColors();
    }

    private void OnSystemThemeChanged(UISettings sender, object args)
    {
        // 该事件在后台线程触发，需切回 UI 线程
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_settings.Theme == AppTheme.System)
            {
                ApplySystemTheme();
                Board.RefreshAll();
            }
        });
    }

    private void UpdateThemeMenuState()
    {
        _themeLightItem.IsChecked = _settings.Theme == AppTheme.Light;
        _themeDarkItem.IsChecked = _settings.Theme == AppTheme.Dark;
        _themeSystemItem.IsChecked = _settings.Theme == AppTheme.System;
    }

    // ---------- 帮助 ----------

    /// <summary>读取当前绑定中某个动作的键位显示文本</summary>
    private string BoundKeys(GameAction action)
    {
        if (_settings.KeyBindings.Bindings.TryGetValue(action, out var keys) && keys.Count > 0)
            return string.Join(" / ", keys.Select(KeyBindingData.KeyName));
        return "未绑定";
    }

    private async void OnHelpClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "扫雷操作说明",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Content = new ScrollViewer
            {
                MaxHeight = 400,
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "鼠标操作",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        },
                        new TextBlock { Text = "左键：翻开格子（首次点击永远不会踩雷）", TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = "右键：循环标记 旗 → 疑问 → 无", TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = "中键 / 左右键同按：快速翻开数字格周围（数字等于旗数时）", TextWrapping = TextWrapping.Wrap },
                        new TextBlock
                        {
                            Text = "键盘操作",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        },
                        new TextBlock
                        {
                            Text = $"移动光标：{BoundKeys(GameAction.MoveLeft)} {BoundKeys(GameAction.MoveUp)} {BoundKeys(GameAction.MoveRight)} {BoundKeys(GameAction.MoveDown)}",
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock { Text = $"翻开格子：{BoundKeys(GameAction.Reveal)}", TextWrapping = TextWrapping.Wrap },
                        new TextBlock
                        {
                            Text = $"插旗：{BoundKeys(GameAction.Flag)}　　疑问标记：{BoundKeys(GameAction.Question)}",
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock { Text = $"智能提示：{BoundKeys(GameAction.Hint)}（自动翻开一个安全格）", TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = $"重新开始：{BoundKeys(GameAction.Restart)}　撤销：{BoundKeys(GameAction.Undo)}（可后悔踩雷）", TextWrapping = TextWrapping.Wrap },
                        new TextBlock
                        {
                            Text = "提示：帮助菜单中的「键位设置…」可自定义全部按键",
                            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            },
        };
        await dialog.ShowAsync();
        Board.Focus(FocusState.Programmatic); // 归还焦点
    }

    // ---------- 键位设置 ----------

    private async void OnKeySettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new KeySettingsDialog(_settings.KeyBindings) { XamlRoot = Content.XamlRoot };
        var result = await dialog.ShowAsync();
        Board.Focus(FocusState.Programmatic); // 归还焦点
        if (result != ContentDialogResult.Primary) return;

        _settings.KeyBindings = dialog.GetBindings();
        SettingsStore.Save(_settings);
        Board.SetKeyBindings(_settings.KeyBindings);
    }

    // ---------- 窗口尺寸 ----------

    /// <summary>窗口尺寸策略：不主动改尺寸（系统默认并居中），保证棋盘随窗口自适应且位置稳定</summary>
    private void SizeWindowToBoard()
    {
        // 保持系统默认窗口尺寸与居中位置；
        // 棋盘尺寸由 GameBoardControl 根据可用空间自适应。
        App.Log($"SizeWindowToBoard: 保持默认窗口尺寸（棋盘 {_engine.Field.Width}x{_engine.Field.Height}）");
    }

    /// <summary>标题栏区域交互（拖拽由 SetTitleBar 系统处理，双击最大化由系统处理）</summary>
    private void OnTitleBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // 拖拽与双击均由系统标题栏机制处理，此处无需额外逻辑
    }
}

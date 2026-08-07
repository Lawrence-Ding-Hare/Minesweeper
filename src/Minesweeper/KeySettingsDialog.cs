using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Minesweeper.Core;
using Windows.System;
using Windows.UI.Core;

namespace Minesweeper;

/// <summary>
/// 键位设置对话框（代码构建，与 BuildMenus 同理规避本机 XamlCompiler 对复杂 XAML 的崩溃问题）。
/// 每个动作最多两个键：点击按键可更换，右键清除第二个键。
/// </summary>
public sealed class KeySettingsDialog : ContentDialog
{
    private readonly KeyBindingData _bindings;      // 工作副本，确定后回写
    private readonly Dictionary<GameAction, (Button First, Button Second)> _chips = new();
    private readonly TextBlock _message;
    private readonly StackPanel _root;

    private GameAction? _captureAction;             // 正在等待按键的动作
    private int _captureSlot;                       // 0=第一个键 1=第二个键

    public KeySettingsDialog(KeyBindingData current)
    {
        Title = "键位设置";
        PrimaryButtonText = "确定";
        CloseButtonText = "取消";
        DefaultButton = ContentDialogButton.Primary;
        CornerRadius = new CornerRadius(8);

        _bindings = (current ?? new KeyBindingData()).Clone();

        var hint = new TextBlock
        {
            Text = "点击按键可更换，右键第二个键可清除，每个动作最多两个键",
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var rows = new StackPanel { Spacing = 8 };
        foreach (GameAction action in Enum.GetValues<GameAction>())
            rows.Children.Add(CreateRow(action));

        var scroll = new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rows,
        };

        _message = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };

        var reset = new Button
        {
            Content = "恢复默认键位",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0),
        };
        reset.Click += (_, _) =>
        {
            _bindings.ResetToDefaults();
            RefreshAllChips();
            ShowMessage("已恢复默认键位", false);
        };

        _root = new StackPanel
        {
            Spacing = 10,
            MinWidth = 380,
            IsTabStop = true, // 捕获按键时接收焦点
        };
        _root.Children.Add(hint);
        _root.Children.Add(scroll);
        _root.Children.Add(_message);
        _root.Children.Add(reset);

        // 捕获模式下在根面板拦截所有按键（先于对话框默认按钮处理）
        _root.KeyDown += OnCaptureKeyDown;

        Content = _root;
    }

    /// <summary>返回编辑后的键位（用户点确定后调用）</summary>
    public KeyBindingData GetBindings() => _bindings;

    // ---------- UI 构建 ----------

    private UIElement CreateRow(GameAction action)
    {
        var first = NewChip(action, 0);
        var second = NewChip(action, 1);

        var row = new Grid
        {
            ColumnSpacing = 8,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = KeyBindingData.ActionName(action),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
        };
        Grid.SetColumn(name, 0);
        Grid.SetColumn(first, 1);
        Grid.SetColumn(second, 2);
        row.Children.Add(name);
        row.Children.Add(first);
        row.Children.Add(second);

        _chips[action] = (first, second);
        return row;
    }

    private Button NewChip(GameAction action, int slot)
    {
        var chip = new Button
        {
            MinWidth = 88,
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 13,
            CornerRadius = new CornerRadius(6),
        };
        chip.Click += (_, _) => BeginCapture(action, slot, chip);
        chip.RightTapped += (_, _) =>
        {
            // 右键清除第二个键（第一个键至少保留一个）
            if (slot == 1 && _bindings.Bindings.TryGetValue(action, out var keys) && keys.Count >= 2)
            {
                keys.RemoveAt(1);
                RefreshChip(action, slot);
                ShowMessage($"已清除「{KeyBindingData.ActionName(action)}」的第二个键", false);
            }
        };
        return chip;
    }

    // ---------- 捕获流程 ----------

    private void BeginCapture(GameAction action, int slot, Button chip)
    {
        _captureAction = action;
        _captureSlot = slot;
        chip.Content = "按下新键…";
        // 焦点移到根面板，避免空格/回车被按钮消费而无法捕获
        _root.Focus(FocusState.Programmatic);
        ShowMessage($"请为「{KeyBindingData.ActionName(action)}」按下新按键（可配合 Ctrl/Alt/Shift，Esc 取消）", false);
    }

    private void EndCapture()
    {
        _captureAction = null;
        RefreshAllChips();
        _message.Visibility = Visibility.Collapsed;
    }

    private void OnCaptureKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_captureAction is not GameAction action) return;
        e.Handled = true; // 捕获模式吞掉所有键，防止触发对话框默认按钮

        var key = e.Key;
        if (key == VirtualKey.Escape)
        {
            EndCapture();
            return;
        }
        // 单独按下修饰键不是完整键位：保持捕获状态等待主键（如 Ctrl+Z 的 Ctrl）
        if (IsModifierKey(key))
        {
            ShowMessage("请继续按下主键（例如 Ctrl+Z 的 Z）", false);
            return;
        }
        if (IsInvalidKey(key))
        {
            ShowMessage($"{KeyBindingData.KeyName((int)key)} 不能绑定，请换一个键", isError: true);
            return;
        }

        // 读取当前修饰键状态组合成键位编码
        bool ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        bool alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(CoreVirtualKeyStates.Down);
        bool shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(CoreVirtualKeyStates.Down);
        int code = KeyBindingData.Encode((int)key, ctrl, alt, shift);

        if (_bindings.FindConflict(code, action) is GameAction conflict)
        {
            ShowMessage($"{KeyBindingData.KeyName(code)} 已被「{KeyBindingData.ActionName(conflict)}」占用，请换一个键", isError: true);
            return;
        }
        AssignKey(action, _captureSlot, code);
        EndCapture();
    }

    private void AssignKey(GameAction action, int slot, int code)
    {
        var keys = _bindings.Bindings[action];
        if (keys.Contains(code)) return; // 同一键位重复绑定 → 无变化
        if (slot == 0)
        {
            if (keys.Count == 0) keys.Add(code);
            else keys[0] = code;
        }
        else
        {
            if (keys.Count < 2) keys.Add(code);
            else keys[1] = code;
        }
    }

    private static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Shift or VirtualKey.Control or VirtualKey.Menu
        or VirtualKey.LeftShift or VirtualKey.RightShift
        or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.LeftMenu or VirtualKey.RightMenu;

    private static bool IsInvalidKey(VirtualKey key) => key switch
    {
        VirtualKey.None
            or VirtualKey.LeftWindows or VirtualKey.RightWindows
            or VirtualKey.CapitalLock or VirtualKey.NumberKeyLock or VirtualKey.Scroll
            or VirtualKey.Tab or VirtualKey.Escape => true,
        >= VirtualKey.GamepadA and <= VirtualKey.GamepadRightThumbstickDown => true,
        _ => false,
    };

    // ---------- 刷新 ----------

    private void RefreshAllChips()
    {
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            RefreshChip(action, 0);
            RefreshChip(action, 1);
        }
    }

    private void RefreshChip(GameAction action, int slot)
    {
        if (!_chips.TryGetValue(action, out var chip) || !_bindings.Bindings.TryGetValue(action, out var keys))
            return;
        var button = slot == 0 ? chip.First : chip.Second;
        string text = slot switch
        {
            0 => keys.Count > 0 ? KeyBindingData.KeyName(keys[0]) : "未设置",
            _ => keys.Count > 1 ? KeyBindingData.KeyName(keys[1]) : "＋",
        };
        button.Content = text;
        ToolTipService.SetToolTip(button, slot == 0
            ? $"更换「{KeyBindingData.ActionName(action)}」按键"
            : "点击设置第二个键 · 右键清除");
    }

    private void ShowMessage(string text, bool isError)
    {
        _message.Text = text;
        _message.Foreground = isError
            ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        _message.Visibility = Visibility.Visible;
    }
}

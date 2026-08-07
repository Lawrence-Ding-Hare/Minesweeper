using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Minesweeper.Core;
using Windows.System;

namespace Minesweeper.Controls;

/// <summary>
/// 扫雷棋盘控件：原生控件组合（Grid + Border + TextBlock + FontIcon），
/// 负责格子渲染、鼠标（左/右/中/双键 chord）与键盘（方向键+快捷键）交互。
/// </summary>
public sealed class GameBoardControl : Grid
{
    public event Action? GameOver;      // 胜利或失败
    public event Action? FirstMove;     // 首次翻开（计时开始）
    public event Action? StateChanged;  // 标记/翻开变化（刷新计数器）
    public event Action? HintRequested; // 按下 H 键
    public event Action? RestartRequested; // 按下 R 键
    public event Action? UndoRequested; // 按下撤销快捷键

    public GameEngine? Engine { get; private set; }

    private readonly Canvas _board;
    private int _cols;
    private int _rows;
    private double _cellSize = 32;

    // 每格渲染元素
    private Grid[,]? _cells;
    private Border[,]? _cellBorders;
    private TextBlock[,]? _numbers;
    private FontIcon[,]? _flags;
    private TextBlock[,]? _mines;
    private TextBlock[,]? _wrongMarks;

    // 键盘光标
    private int _cursorX;
    private int _cursorY;
    private bool _hasCursor = true;

    // 键位绑定（键码 → 动作 反查表，先绑定先生效）
    private KeyBindingData _bindings = new();
    private Dictionary<int, GameAction> _keyLookup = new();

    // 双键 chord 检测（按住左键时再按右键）
    private bool _leftPressed;
    private bool _rightPressed;
    private bool _chordTriggered;

    public GameBoardControl()
    {
        IsTabStop = true; // 可聚焦接收键盘（Tab 导航与辅助功能）
        Background = new SolidColorBrush(Colors.Transparent);
        PointerPressed += (_, _) => Focus(FocusState.Programmatic); // 空白区域点击聚焦

        _board = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_board);
        SizeChanged += (_, _) => RecalculateLayout();
    }

    // ---------- 生命周期 ----------

    /// <summary>开始新一局（重建棋盘）</summary>
    public void StartNewGame(GameEngine engine)
    {
        Engine = engine;
        _leftPressed = false;
        _rightPressed = false;
        _chordTriggered = false;
        _cursorX = 0;
        _cursorY = 0;
        BuildBoard();
        Focus(FocusState.Programmatic);
    }

    private void BuildBoard()
    {
        _board.Children.Clear();
        _cols = Engine!.Field.Width;
        _rows = Engine.Field.Height;

        _cells = new Grid[_cols, _rows];
        _cellBorders = new Border[_cols, _rows];
        _numbers = new TextBlock[_cols, _rows];
        _flags = new FontIcon[_cols, _rows];
        _mines = new TextBlock[_cols, _rows];
        _wrongMarks = new TextBlock[_cols, _rows];

        RecalculateLayout();
        App.Log($"BuildBoard: {_cols}x{_rows} cellSize={_cellSize:F1} boardW={_board.Width:F0} boardH={_board.Height:F0} actualW={ActualWidth:F0} actualH={ActualHeight:F0}");
        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                _board.Children.Add(CreateCell(x, y));
            }
        }
        App.Log($"BuildBoard: 完成，格子 {_cols * _rows} 个");
        RefreshAll();

        // 布局完成后重算一次棋盘尺寸（避免布局未完成时棋盘缩到最小）
        DispatcherQueue.TryEnqueue(() =>
        {
            RecalculateLayout();
            App.Log($"BuildBoard 延迟重算: cellSize={_cellSize:F1} actualW={ActualWidth:F0}");
        });
    }

    private Grid CreateCell(int x, int y)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("CellCoveredBorderBrush"),
            CornerRadius = new CornerRadius(2),
        };
        _cellBorders![x, y] = border;

        // 数字（1-8 与问号共用）
        var number = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _numbers![x, y] = number;

        // 旗
        var flag = new FontIcon
        {
            Glyph = "", // Segoe Fluent Icons: Flag
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _flags![x, y] = flag;

        // 雷（emoji）
        var mine = new TextBlock
        {
            Text = "💣",
            FontFamily = new FontFamily("Segoe UI Emoji"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _mines![x, y] = mine;

        // 错误旗标记（失败时显示 ❌）
        var wrong = new TextBlock
        {
            Text = "❌",
            FontFamily = new FontFamily("Segoe UI Emoji"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _wrongMarks![x, y] = wrong;

        var content = new Grid { IsHitTestVisible = false };
        content.Children.Add(number);
        content.Children.Add(flag);
        content.Children.Add(mine);
        content.Children.Add(wrong);

        var cell = new Grid { IsTabStop = false };
        cell.Children.Add(border);
        cell.Children.Add(content);

        AutomationProperties.SetName(cell, $"第 {y + 1} 行第 {x + 1} 列");

        cell.PointerPressed += (_, e) => OnCellPointerPressed(x, y, e);
        cell.PointerMoved += (_, e) => OnCellPointerMoved(x, y, e);
        cell.PointerReleased += (_, e) => OnCellPointerReleased(e);
        cell.PointerCanceled += (_, _) => ResetButtons();

        _cells![x, y] = cell;
        return cell;
    }

    /// <summary>按当前实际主题查找画笔资源（元素级主题驱动，避免 Application.RequestedTheme 限制）</summary>
    private Brush GetThemeBrush(string key)
    {
        var dicts = Application.Current.Resources.ThemeDictionaries;
        string themeKey = ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        if (dicts.TryGetValue(themeKey, out var rd) && rd is ResourceDictionary d &&
            d.TryGetValue(key, out var v) && v is Brush b)
            return b;
        // 回退：直接查合并字典
        return Application.Current.Resources[key] as Brush
               ?? new SolidColorBrush(Colors.Transparent);
    }

    // ---------- 布局 ----------

    private void RecalculateLayout()
    {
        try
        {
            RecalculateLayoutCore();
        }
        catch (Exception ex)
        {
            App.Log($"RecalculateLayout 异常: {ex.Message} | Engine={Engine != null} cols={_cols} rows={_rows} cells={_cells != null} numbers={_numbers != null} app={Application.Current != null}");
            throw;
        }
    }

    private void RecalculateLayoutCore()
    {
        if (Engine == null || _cols == 0) return;

        const double padding = 8;
        // 布局未完成时（ActualWidth=0）用合理默认尺寸，避免棋盘缩到最小
        double availW = ActualWidth > 0 ? Math.Max(ActualWidth - padding * 2, 100) : 400;
        double availH = ActualHeight > 0 ? Math.Max(ActualHeight - padding * 2, 100) : 500;

        _cellSize = Math.Clamp(Math.Min(availW / _cols, availH / _rows), 14, 48);

        _board.Width = _cols * _cellSize;
        _board.Height = _rows * _cellSize;
        var bg = GetThemeBrush("BoardBackgroundBrush");
        if (bg == null)
        {
            App.Log("RecalculateLayout: BoardBackgroundBrush 资源未找到!");
        }
        _board.Background = bg;

        if (_cells == null)
        {
            App.Log($"RecalculateLayout: _cells 为 null (Engine={Engine != null}, cols={_cols})");
            return;
        }
        for (int y = 0; y < _rows; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                var cell = _cells[x, y];
                if (cell == null) continue; // 格子尚未创建（BuildBoard 先布局后建格）

                cell.Width = _cellSize;
                cell.Height = _cellSize;
                Canvas.SetLeft(cell, x * _cellSize);
                Canvas.SetTop(cell, y * _cellSize);

                double fontSize = Math.Clamp(_cellSize * 0.58, 9, 27);
                _numbers![x, y].FontSize = fontSize;
                _flags![x, y].FontSize = fontSize;
                _mines![x, y].FontSize = _cellSize * 0.8;
                _wrongMarks![x, y].FontSize = _cellSize * 0.62;
            }
        }
    }

    // ---------- 鼠标交互 ----------

    private void OnCellPointerPressed(int x, int y, PointerRoutedEventArgs e)
    {
        if (Engine == null) return;
        Focus(FocusState.Programmatic);
        SetCursor(x, y);
        _chordTriggered = false;

        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed) _leftPressed = true;
        if (props.IsRightButtonPressed) _rightPressed = true;

        int index = y * _cols + x;

        if (_leftPressed && _rightPressed)
        {
            // 左右键同按 = chord
            DoChord(index);
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            RevealCellAt(index);
        }
        else if (props.IsRightButtonPressed)
        {
            Engine.ToggleMark(index);
            StateChanged?.Invoke();
            RefreshAll();
        }
        else if (props.IsMiddleButtonPressed)
        {
            DoChord(index);
        }
    }

    private void OnCellPointerMoved(int x, int y, PointerRoutedEventArgs e)
    {
        // 按住左键再按右键 = chord（经典扫雷操作）
        if (_leftPressed && _rightPressed && !_chordTriggered)
        {
            _chordTriggered = true;
            DoChord(y * _cols + x);
        }
    }

    private void OnCellPointerReleased(PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) _leftPressed = false;
        if (!props.IsRightButtonPressed) _rightPressed = false;
    }

    private void ResetButtons()
    {
        _leftPressed = false;
        _rightPressed = false;
    }

    private void DoChord(int index)
    {
        if (Engine == null) return;
        var result = Engine.ChordCell(index);
        if (result != ChordResult.NoOp)
        {
            HandleRevealResult(result == ChordResult.Win, result == ChordResult.Mine);
        }
    }

    private void RevealCellAt(int index)
    {
        if (Engine == null) return;
        bool wasReady = Engine.Phase == GamePhase.Ready;
        var result = Engine.RevealCell(index);
        if (result == RevealResult.NoOp) return;
        if (wasReady && Engine.Phase == GamePhase.Playing) FirstMove?.Invoke();
        HandleRevealResult(result == RevealResult.Win, result == RevealResult.Mine);
    }

    private void HandleRevealResult(bool won, bool lost)
    {
        StateChanged?.Invoke();
        if (won)
        {
            Engine!.MarkAllMinesAsFlagged();
            RefreshAll();
            GameOver?.Invoke();
        }
        else if (lost)
        {
            RefreshAll();
            GameOver?.Invoke();
        }
        else
        {
            RefreshAll();
        }
    }

    // ---------- 键盘交互 ----------

    /// <summary>设置键位绑定（可随时更换，立即生效）</summary>
    public void SetKeyBindings(KeyBindingData bindings)
    {
        _bindings = bindings ?? new KeyBindingData();
        _keyLookup = new Dictionary<int, GameAction>();
        // 按动作枚举顺序建反查表：同一键位被多个动作占用时，先声明的动作先生效
        foreach (GameAction action in Enum.GetValues<GameAction>())
        {
            if (!_bindings.Bindings.TryGetValue(action, out var keys)) continue;
            foreach (int code in keys)
                _keyLookup.TryAdd(code, action);
        }
    }

    /// <summary>
    /// 处理一个按键（由窗口根元素的 KeyDown 路由，无论焦点在哪个控件都能响应）。
    /// 修饰键状态由调用方提供（WinUI 3 的 KeyRoutedEventArgs 无修饰键属性，需查输入状态）。
    /// 返回是否消费了该键。
    /// </summary>
    public bool HandleKey(VirtualKey key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        if (Engine == null || key == VirtualKey.None) return false;
        int code = KeyBindingData.Encode((int)key, ctrl, alt, shift);
        if (!_keyLookup.TryGetValue(code, out var action)) return false;

        switch (action)
        {
            case GameAction.MoveLeft:
                MoveCursor(-1, 0);
                break;
            case GameAction.MoveRight:
                MoveCursor(1, 0);
                break;
            case GameAction.MoveUp:
                MoveCursor(0, -1);
                break;
            case GameAction.MoveDown:
                MoveCursor(0, 1);
                break;
            case GameAction.Reveal:
                RevealCellAt(_cursorY * _cols + _cursorX);
                break;
            case GameAction.Flag:
                Engine.ToggleFlag(_cursorY * _cols + _cursorX);
                StateChanged?.Invoke();
                RefreshAll();
                break;
            case GameAction.Question:
                Engine.ToggleQuestion(_cursorY * _cols + _cursorX);
                StateChanged?.Invoke();
                RefreshAll();
                break;
            case GameAction.Hint:
                HintRequested?.Invoke();
                break;
            case GameAction.Restart:
                RestartRequested?.Invoke();
                break;
            case GameAction.Undo:
                UndoRequested?.Invoke();
                break;
        }
        return true;
    }

    private void MoveCursor(int dx, int dy)
    {
        _cursorX = Math.Clamp(_cursorX + dx, 0, _cols - 1);
        _cursorY = Math.Clamp(_cursorY + dy, 0, _rows - 1);
        RefreshAll();
    }

    private void SetCursor(int x, int y)
    {
        _cursorX = x;
        _cursorY = y;
    }

    public void SetCursorIndex(int index)
    {
        if (_cols == 0) return;
        _cursorX = index % _cols;
        _cursorY = index / _cols;
        RefreshAll();
    }

    // ---------- 提示 ----------

    /// <summary>执行一次提示：返回是否成功提示</summary>
    public bool ApplyHint()
    {
        if (Engine == null) return false;
        int hint = Engine.GetHint();
        if (hint < 0) return false;
        SetCursorIndex(hint);
        RevealCellAt(hint);
        return true;
    }

    // ---------- 渲染 ----------

    public void RefreshAll()
    {
        if (Engine == null || _cells == null) return;
        for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
                RefreshCell(x, y);
    }

    private void RefreshCell(int x, int y)
    {
        int index = y * _cols + x;
        bool revealed = Engine!.Field.IsRevealed(index);
        bool isMine = Engine.Field.IsMine(index);
        CellMark mark = Engine.Field.GetMark(index);

        var border = _cellBorders![x, y];
        var number = _numbers![x, y];
        var flag = _flags![x, y];
        var mine = _mines![x, y];
        var wrong = _wrongMarks![x, y];

        bool isCursor = _hasCursor && x == _cursorX && y == _cursorY;

        number.Visibility = Visibility.Collapsed;
        flag.Visibility = Visibility.Collapsed;
        mine.Visibility = Visibility.Collapsed;
        wrong.Visibility = Visibility.Collapsed;

        if (revealed)
        {
            border.Background = GetThemeBrush("CellRevealedBrush");
            border.BorderBrush = GetThemeBrush("CellRevealedBorderBrush");

            if (isMine)
            {
                mine.Visibility = Visibility.Visible;
                if (Engine.Phase == GamePhase.Lost)
                    border.Background = GetThemeBrush("MineHitBrush"); // 踩中的雷红底
            }
            else
            {
                int adj = Engine.Field.AdjacentMines(index);
                if (adj > 0)
                {
                    number.Text = adj.ToString();
                    number.Foreground = GetThemeBrush($"MineNumber{adj}Brush");
                    number.Visibility = Visibility.Visible;
                }
            }
        }
        else
        {
            border.Background = GetThemeBrush("CellCoveredBrush");
            border.BorderBrush = GetThemeBrush("CellCoveredBorderBrush");

            switch (mark)
            {
                case CellMark.Flag:
                    flag.Visibility = Visibility.Visible;
                    flag.Foreground = GetThemeBrush("FlagBrush");
                    // 失败时错误旗显示 ❌（标在不该标的格上）
                    if (Engine.Phase == GamePhase.Lost && !isMine)
                    {
                        flag.Visibility = Visibility.Collapsed;
                        wrong.Visibility = Visibility.Visible;
                    }
                    break;
                case CellMark.Question:
                    number.Text = "?";
                    number.Foreground = GetThemeBrush("QuestionBrush");
                    number.Visibility = Visibility.Visible;
                    break;
            }
        }

        // 光标高亮
        if (isCursor)
        {
            border.BorderBrush = GetThemeBrush("CursorBrush");
            border.BorderThickness = new Thickness(2);
        }
        else if (border.BorderThickness != new Thickness(1))
        {
            border.BorderThickness = new Thickness(1);
        }
    }
}

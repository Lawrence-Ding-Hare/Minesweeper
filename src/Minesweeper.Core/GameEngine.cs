namespace Minesweeper.Core;

/// <summary>游戏会话阶段</summary>
public enum GamePhase
{
    Ready,    // 等待首次操作
    Playing,  // 进行中
    Won,      // 胜利
    Lost,     // 失败
}

/// <summary>
/// 会话状态机：连接 UI 与棋盘，管理阶段流转与提示系统。
/// 计时由 UI 层负责（显示专用），引擎不持有时钟。
/// </summary>
public sealed class GameEngine
{
    public Minefield Field { get; }
    public GamePhase Phase { get; private set; }
    public int TotalMines => Field.MineCount;
    public int RemainingMines => Field.MineCount - Field.FlagCount;
    public bool IsOver => Phase is GamePhase.Won or GamePhase.Lost;

    /// <summary>撤销栈上限（防止内存无限增长）</summary>
    public const int MaxUndoSteps = 50;

    /// <summary>撤销栈：每次有效操作前压入 (操作前阶段, 操作前棋盘状态)</summary>
    private readonly List<(GamePhase Phase, Minefield.Snapshot Field)> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public GameEngine(int width, int height, int mineCount, Random? random = null)
    {
        Field = new Minefield(width, height, mineCount, random);
        Phase = GamePhase.Ready;
    }

    public GameEngine(DifficultySpec spec, Random? random = null)
        : this(spec.Width, spec.Height, spec.Mines, random) { }

    /// <summary>翻开格子。Ready 阶段首击自动进入 Playing。有效操作会压入撤销栈。</summary>
    public RevealResult RevealCell(int index)
    {
        if (IsOver) return RevealResult.NoOp;
        var phaseBefore = Phase;
        var snap = Field.CaptureSnapshot();
        RevealResult result = Field.Reveal(index);
        if (result == RevealResult.NoOp) return result;
        PushUndo(phaseBefore, snap);
        if (Phase == GamePhase.Ready) Phase = GamePhase.Playing;
        if (result == RevealResult.Mine) Phase = GamePhase.Lost;
        else if (result == RevealResult.Win) Phase = GamePhase.Won;
        return result;
    }

    /// <summary>Chord 操作（双击数字格）。Ready 阶段不允许 chord。有效操作会压入撤销栈。</summary>
    public ChordResult ChordCell(int index)
    {
        if (IsOver) return ChordResult.NoOp;
        var phaseBefore = Phase;
        var snap = Field.CaptureSnapshot();
        ChordResult result = Field.Chord(index);
        if (result == ChordResult.NoOp) return result;
        PushUndo(phaseBefore, snap);
        if (result == ChordResult.Mine) Phase = GamePhase.Lost;
        else if (result == ChordResult.Win) Phase = GamePhase.Won;
        return result;
    }

    /// <summary>标记循环（无→旗→疑问→无）。游戏中与开局前均可标记。有效变化会压入撤销栈。</summary>
    public CellMark ToggleMark(int index)
    {
        if (IsOver) return CellMark.None;
        var before = Field.GetMark(index);
        var snap = Field.CaptureSnapshot();
        var mark = Field.CycleMark(index);
        if (mark != before) PushUndo(Phase, snap);
        return mark;
    }

    /// <summary>快捷键 F：切换旗标记（无→旗→无）。有效变化会压入撤销栈。</summary>
    public CellMark ToggleFlag(int index)
    {
        if (IsOver) return CellMark.None;
        var before = Field.GetMark(index);
        var snap = Field.CaptureSnapshot();
        var mark = Field.SetMark(index, Field.GetMark(index) == CellMark.Flag ? CellMark.None : CellMark.Flag);
        if (mark != before) PushUndo(Phase, snap);
        return mark;
    }

    /// <summary>快捷键 Q：切换疑问标记（无→疑问→无）。有效变化会压入撤销栈。</summary>
    public CellMark ToggleQuestion(int index)
    {
        if (IsOver) return CellMark.None;
        var before = Field.GetMark(index);
        var snap = Field.CaptureSnapshot();
        var mark = Field.SetMark(index, Field.GetMark(index) == CellMark.Question ? CellMark.None : CellMark.Question);
        if (mark != before) PushUndo(Phase, snap);
        return mark;
    }

    /// <summary>胜利时：把所有未翻开的雷格标为旗（经典胜利展示）</summary>
    public void MarkAllMinesAsFlagged()
    {
        if (Phase != GamePhase.Won) return;
        Field.MarkAllMinesAsFlagged();
    }

    /// <summary>
    /// 提示系统：返回一个确定安全的格子索引（翻开不会炸）。
    /// 策略：优先找与已翻开数字格相邻的未翻开非雷格（有信息量），
    /// 其次返回未翻开非雷非旗格；无可用格返回 -1。
    /// </summary>
    public int GetHint()
    {
        if (IsOver) return -1;

        // 1. 确定性推断：数字格邻居中未翻开、非雷、未插旗的格（Playing 阶段）
        if (Phase == GamePhase.Playing)
        {
            for (int i = 0; i < Field.TotalCells; i++)
            {
                if (!Field.IsRevealed(i)) continue;
                if (Field.AdjacentMines(i) == 0) continue; // 0 格没有信息量
                foreach (int n in Field.GetNeighborsOf(i))
                {
                    if (!Field.IsRevealed(n) && !Field.IsMine(n) && Field.GetMark(n) != CellMark.Flag)
                        return n;
                }
            }
        }

        // 2. 兜底：未翻开、非雷、非旗的格（Ready 阶段雷板未生成，全部安全）
        for (int i = 0; i < Field.TotalCells; i++)
        {
            if (!Field.IsRevealed(i) && !Field.IsMine(i) && Field.GetMark(i) != CellMark.Flag)
                return i;
        }
        return -1;
    }

    /// <summary>重开一局（同一规格，清空撤销栈）</summary>
    public void Restart()
    {
        Field.Reset();
        Phase = GamePhase.Ready;
        ClearUndo();
    }

    // ---------- 撤销 ----------

    private void PushUndo(GamePhase phaseBefore, Minefield.Snapshot snapshot)
    {
        _undoStack.Add((phaseBefore, snapshot));
        if (_undoStack.Count > MaxUndoSteps)
            _undoStack.RemoveAt(0); // 超出上限丢弃最旧的一步
    }

    /// <summary>撤销一步：恢复上一步操作前的棋盘与阶段（含"后悔踩雷"：游戏结束后仍可撤销）。</summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;
        var (phase, snap) = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        Phase = phase;
        Field.RestoreSnapshot(snap);
        return true;
    }

    /// <summary>清空撤销栈（新一局/重开时）</summary>
    public void ClearUndo() => _undoStack.Clear();
}

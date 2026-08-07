using Xunit;

namespace Minesweeper.Core.Tests;

public class UndoTests
{
    // ---------- 基础撤销 ----------

    [Fact]
    public void Undo_EmptyStack_ReturnsFalse()
    {
        var engine = new GameEngine(9, 9, 10, new Random(1));
        Assert.False(engine.CanUndo);
        Assert.False(engine.Undo());
    }

    [Fact]
    public void Undo_RestoresReveal()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.RevealCell(0); // 首击
        Assert.True(engine.CanUndo);
        Assert.True(engine.Undo());
        Assert.Equal(GamePhase.Ready, engine.Phase);
        Assert.Equal(0, engine.Field.RevealedCount);
    }

    [Fact]
    public void Undo_RestoresFlag()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.ToggleFlag(5);
        Assert.Equal(1, engine.Field.FlagCount);
        Assert.True(engine.Undo());
        Assert.Equal(0, engine.Field.FlagCount);
        Assert.Equal(CellMark.None, engine.Field.GetMark(5));
    }

    [Fact]
    public void Undo_MultipleSteps_ReplaysInReverseOrder()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.ToggleFlag(1);
        engine.ToggleFlag(2);
        engine.ToggleFlag(3);
        Assert.Equal(3, engine.Field.FlagCount);
        Assert.True(engine.Undo());
        Assert.Equal(2, engine.Field.FlagCount);
        Assert.True(engine.Undo());
        Assert.Equal(1, engine.Field.FlagCount);
        Assert.True(engine.Undo());
        Assert.Equal(0, engine.Field.FlagCount);
        Assert.False(engine.CanUndo);
    }

    // ---------- 后悔踩雷：游戏结束后仍可撤销 ----------

    [Fact]
    public void Undo_AfterLosingMine_ResumesPlaying()
    {
        var engine = new GameEngine(9, 9, 10, new Random(3));
        engine.RevealCell(0);
        // 找到一颗雷踩中
        for (int i = 0; i < engine.Field.TotalCells; i++)
        {
            if (!engine.Field.IsMine(i)) continue;
            Assert.Equal(RevealResult.Mine, engine.RevealCell(i));
            break;
        }
        Assert.Equal(GamePhase.Lost, engine.Phase);
        Assert.True(engine.Undo());
        Assert.NotEqual(GamePhase.Lost, engine.Phase);
        Assert.False(engine.Field.IsRevealed(engine.Field.TotalCells - 1)); // 棋盘仍在
    }

    [Fact]
    public void Undo_AfterWin_ResumesPlaying()
    {
        var engine = new GameEngine(5, 5, 3, new Random(5));
        engine.RevealCell(0);      // 首击
        // 给一颗雷插旗（额外一步，保证撤销栈不止一步；雷格无需翻开，不影响胜利）
        for (int i = 0; i < engine.Field.TotalCells; i++)
        {
            if (engine.Field.IsMine(i)) { engine.ToggleFlag(i); break; }
        }
        // 逐个翻开非雷格直到胜利
        for (int i = 0; i < engine.Field.TotalCells; i++)
        {
            if (engine.Field.IsMine(i)) continue;
            engine.RevealCell(i);
        }
        Assert.Equal(GamePhase.Won, engine.Phase);
        Assert.True(engine.Undo());
        Assert.Equal(GamePhase.Playing, engine.Phase); // 恢复到制胜一步之前
        Assert.False(engine.IsOver);
    }

    // ---------- 无效操作不入栈 ----------

    [Fact]
    public void Undo_NoOpOperations_DoNotPush()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.RevealCell(0); // 首击
        // 重复翻开已翻开的格 = NoOp，不应产生撤销步
        Assert.Equal(RevealResult.NoOp, engine.RevealCell(0));
        Assert.True(engine.Undo());
        Assert.False(engine.CanUndo); // 只有首击一步
    }

    [Fact]
    public void Undo_SameMarkToggle_DoesNotPush()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        // 在已翻开的格上 F/Q 均无效（SetMark 返回 None 且无变化）
        engine.RevealCell(0);
        Assert.Equal(CellMark.None, engine.ToggleFlag(0));
        Assert.True(engine.Undo());
        Assert.False(engine.CanUndo);
    }

    // ---------- 栈上限与重置 ----------

    [Fact]
    public void Undo_StackLimitedToMaxSteps()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        for (int i = 0; i < GameEngine.MaxUndoSteps + 20; i++)
            engine.ToggleFlag(i % engine.Field.TotalCells);
        int count = 0;
        while (engine.Undo()) count++;
        Assert.Equal(GameEngine.MaxUndoSteps, count);
    }

    [Fact]
    public void Undo_UndoItself_DoesNotPush()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.ToggleFlag(1);
        engine.Undo();
        Assert.False(engine.CanUndo); // 撤销不产生新撤销步
    }

    [Fact]
    public void Restart_ClearsUndoStack()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        engine.ToggleFlag(1);
        Assert.True(engine.CanUndo);
        engine.Restart();
        Assert.False(engine.CanUndo);
    }

    // ---------- 组合键编码 ----------

    [Fact]
    public void Encode_RoundTripsModifiers()
    {
        int code = KeyBindingData.Encode(90, ctrl: true); // Ctrl+Z
        Assert.Equal(90, KeyBindingData.KeyOf(code));
        Assert.True(KeyBindingData.HasCtrl(code));
        Assert.False(KeyBindingData.HasAlt(code));
        Assert.False(KeyBindingData.HasShift(code));
        Assert.Equal("Ctrl+Z", KeyBindingData.KeyName(code));
    }

    [Fact]
    public void Encode_PlainKey_IsBackwardCompatible()
    {
        int code = KeyBindingData.Encode(70); // F
        Assert.Equal(70, code);
        Assert.Equal("F", KeyBindingData.KeyName(code));
    }

    [Fact]
    public void Defaults_IncludeCtrlZForUndo()
    {
        var kb = new KeyBindingData();
        int code = Assert.Single(kb.Bindings[GameAction.Undo]);
        Assert.True(KeyBindingData.HasCtrl(code));
        Assert.Equal(90, KeyBindingData.KeyOf(code));
    }

    [Fact]
    public void FindConflict_DistinguishesModifiers()
    {
        var kb = new KeyBindingData();
        // 默认绑定：Z 无绑定（纯 Ctrl+Z 是撤销），70 是 F
        Assert.Null(kb.FindConflict(KeyBindingData.Encode(90), GameAction.Undo));      // 纯 Z 无冲突
        Assert.Equal(GameAction.Undo, kb.FindConflict(KeyBindingData.Encode(90, ctrl: true), GameAction.Flag)); // Ctrl+Z 已占用
    }
}

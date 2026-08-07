using Xunit;

namespace Minesweeper.Core.Tests;

public class GameEngineTests
{
    // ---------- 阶段流转 ----------

    [Fact]
    public void Phase_StartsReady_ThenPlaying_AfterFirstReveal()
    {
        var engine = new GameEngine(9, 9, 10, new Random(7));
        Assert.Equal(GamePhase.Ready, engine.Phase);
        Assert.Equal(RevealResult.Revealed, engine.RevealCell(0));
        Assert.Equal(GamePhase.Playing, engine.Phase);
    }

    [Fact]
    public void Phase_FirstRevealOnMine_IsNeverLost_BecauseFirstClickSafe()
    {
        // 首击永不踩雷：任何首击都进入 Playing
        for (int seed = 0; seed < 50; seed++)
        {
            var engine = new GameEngine(9, 9, 10, new Random(seed));
            Assert.Equal(RevealResult.Revealed, engine.RevealCell(seed % 81));
            Assert.Equal(GamePhase.Playing, engine.Phase);
        }
    }

    [Fact]
    public void Phase_HitMine_GoesLost()
    {
        var engine = new GameEngine(9, 9, 10, new Random(3));
        engine.RevealCell(0);
        // 找到一颗雷并踩中
        for (int i = 0; i < engine.Field.TotalCells; i++)
        {
            if (!engine.Field.IsMine(i)) continue;
            Assert.Equal(RevealResult.Mine, engine.RevealCell(i));
            Assert.Equal(GamePhase.Lost, engine.Phase);
            return;
        }
        throw new Xunit.Sdk.XunitException("布局中应有雷");
    }

    [Fact]
    public void Phase_OperationsBlocked_AfterGameOver()
    {
        var engine = new GameEngine(9, 9, 10, new Random(3));
        engine.RevealCell(0);
        for (int i = 0; i < engine.Field.TotalCells; i++)
        {
            if (!engine.Field.IsMine(i)) continue;
            engine.RevealCell(i); // 踩雷 → Lost
            break;
        }

        Assert.Equal(GamePhase.Lost, engine.Phase);
        Assert.Equal(RevealResult.NoOp, engine.RevealCell(5));
        Assert.Equal(ChordResult.NoOp, engine.ChordCell(5));
        Assert.Equal(CellMark.None, engine.ToggleMark(5));
    }

    [Fact]
    public void Phase_Win_WhenAllRevealed()
    {
        var engine = new GameEngine(5, 5, 1, new Random(2));
        engine.RevealCell(12);
        // 雷在未知位置，找出来并避开
        int mine = -1;
        for (int i = 0; i < 25; i++) if (engine.Field.IsMine(i)) { mine = i; break; }
        for (int i = 0; i < 25; i++)
        {
            if (i == mine) continue;
            if (engine.Field.IsRevealed(i)) continue;
            if (engine.RevealCell(i) == RevealResult.Win) break;
        }
        Assert.Equal(GamePhase.Won, engine.Phase);
        Assert.True(engine.IsOver);
    }

    // ---------- 标记与雷数 ----------

    [Fact]
    public void RemainingMines_UpdatesWithFlags()
    {
        var engine = new GameEngine(9, 9, 10, new Random(1));
        Assert.Equal(10, engine.RemainingMines);
        engine.ToggleMark(1);
        Assert.Equal(9, engine.RemainingMines);
        engine.ToggleMark(1); // 旗 → 问号，雷数恢复
        Assert.Equal(10, engine.RemainingMines);
        engine.ToggleMark(1); // 问号 → 无
        engine.ToggleMark(1); // 旗
        engine.ToggleMark(2); // 旗
        Assert.Equal(8, engine.RemainingMines);
    }

    [Fact]
    public void RemainingMines_CanGoNegative()
    {
        var engine = new GameEngine(9, 9, 2, new Random(1));
        for (int i = 0; i < 5; i++) engine.ToggleMark(i);
        Assert.Equal(-3, engine.RemainingMines);
    }

    // ---------- 提示系统 ----------

    [Fact]
    public void Hint_BeforeStart_ReturnsHiddenCell()
    {
        var engine = new GameEngine(9, 9, 10, new Random(5));
        int hint = engine.GetHint();
        Assert.InRange(hint, 0, 80);
        Assert.False(engine.Field.IsRevealed(hint));
    }

    [Fact]
    public void Hint_IsAlwaysSafe_AcrossSeeds()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var engine = new GameEngine(16, 16, 40, new Random(seed));
            engine.RevealCell(seed % 256); // 首击
            for (int step = 0; step < 10; step++)
            {
                int hint = engine.GetHint();
                if (hint < 0) break; // 无可用格
                Assert.False(engine.Field.IsMine(hint), $"seed={seed} 提示返回雷格");
                Assert.False(engine.Field.IsRevealed(hint));
                engine.RevealCell(hint); // 提示翻开必不炸
            }
            // 10 步提示全部安全且引擎仍可玩
            Assert.NotEqual(GamePhase.Lost, engine.Phase);
        }
    }

    [Fact]
    public void Hint_AfterGameOver_ReturnsMinusOne()
    {
        var engine = new GameEngine(9, 9, 10, new Random(3));
        engine.RevealCell(0);
        for (int i = 0; i < engine.Field.TotalCells; i++)
            if (engine.Field.IsMine(i)) { engine.RevealCell(i); break; }
        Assert.Equal(-1, engine.GetHint());
    }

    [Fact]
    public void Hint_SkipsFlaggedCells()
    {
        // 全盘插旗后只剩一个未标记格 → 提示只能返回该格
        var engine = new GameEngine(9, 9, 10, new Random(9));
        engine.RevealCell(0);
        for (int i = 0; i < engine.Field.TotalCells; i++)
            if (!engine.Field.IsRevealed(i)) engine.ToggleMark(i);
        Assert.Equal(-1, engine.GetHint());
    }

    // ---------- 重开 ----------

    [Fact]
    public void Restart_ResetsPhaseAndField()
    {
        var engine = new GameEngine(9, 9, 10, new Random(4));
        engine.RevealCell(0);
        engine.ToggleMark(1);
        Assert.Equal(GamePhase.Playing, engine.Phase);

        engine.Restart();
        Assert.Equal(GamePhase.Ready, engine.Phase);
        Assert.Equal(0, engine.Field.RevealedCount);
        Assert.Equal(0, engine.Field.FlagCount);
        Assert.False(engine.Field.MinesGenerated);
    }
}

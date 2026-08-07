using Minesweeper.Core;
using Xunit;

namespace Minesweeper.Core.Tests;

public class MinefieldTests
{
    /// <summary>按雷位索引构造布局</summary>
    private static bool[] Layout(int width, int height, params int[] mineIndices)
    {
        var layout = new bool[width * height];
        foreach (int i in mineIndices) layout[i] = true;
        return layout;
    }

    // ---------- 雷板生成 ----------

    [Theory]
    [InlineData(9, 9, 10)]
    [InlineData(16, 16, 40)]
    [InlineData(30, 16, 99)]
    [InlineData(3, 3, 8)]
    [InlineData(40, 40, 1439)] // 90% 上限
    public void GenerateMines_PlacesExactCount(int w, int h, int mines)
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var field = new Minefield(w, h, mines, new Random(seed));
            field.Reveal(0);
            int count = 0;
            for (int i = 0; i < field.TotalCells; i++)
                if (field.IsMine(i)) count++;
            Assert.Equal(mines, count);
        }
    }

    [Fact]
    public void GenerateMines_FirstClickArea_IsSafe()
    {
        // 多个种子 × 多个首击位置：首击格及其 8 邻居均无雷
        for (int seed = 0; seed < 25; seed++)
        {
            var field = new Minefield(16, 16, 40, new Random(seed));
            int safe = 5 * 16 + 7; // (7,5)
            field.Reveal(safe);
            Assert.False(field.IsMine(safe), $"seed={seed} 首击格有雷");
            foreach (int n in field.GetNeighborsOf(safe))
                Assert.False(field.IsMine(n), $"seed={seed} 首击邻居有雷 (idx={n})");
        }
    }

    [Fact]
    public void GenerateMines_ExtremeSmallBoard_SafeCellAlwaysSafe()
    {
        // 3x3 8 雷（雷数接近满盘）：排除区收缩后首击格本身仍安全
        for (int seed = 0; seed < 50; seed++)
        {
            var field = new Minefield(3, 3, 8, new Random(seed));
            field.Reveal(4); // 中心
            Assert.False(field.IsMine(4));
        }
    }

    [Fact]
    public void GenerateMines_OnceOnly()
    {
        var field = new Minefield(9, 9, 10, new Random(1));
        field.Reveal(0);
        var first = field.IsMine(80);
        field.Reveal(1);
        // 已生成后再次 Reveal 不再改变雷板
        Assert.Equal(first, field.IsMine(80));
    }

    // ---------- 邻居与数字 ----------

    [Fact]
    public void Neighbors_CountIsCorrect_AtEdgesAndCorners()
    {
        var field = new Minefield(9, 9, 10, new Random(1));
        Assert.Equal(3, field.GetNeighborsOf(0).Count());          // 左上角
        Assert.Equal(5, field.GetNeighborsOf(0 + 4).Count());      // 上边中间
        Assert.Equal(5, field.GetNeighborsOf(4 * 9 + 0).Count());  // 左边中间
        Assert.Equal(8, field.GetNeighborsOf(4 * 9 + 4).Count());  // 中间
        Assert.Equal(3, field.GetNeighborsOf(8 * 9 + 8).Count());  // 右下角
    }

    [Fact]
    public void AdjacentMines_CountsCorrectly()
    {
        // 布局：雷在 (1,0)=1、(0,1)=9、(2,2)=20
        var field = new Minefield(9, 9, Layout(9, 9, 1, 9, 20));
        Assert.Equal(3, field.AdjacentMines(10)); // (1,1) 周围有 (1,0)(0,1)(2,2)
        Assert.Equal(2, field.AdjacentMines(0));  // 角落有 (1,0) 与 (0,1)
        Assert.Equal(2, field.AdjacentMines(19)); // (1,2) 周围有 (0,1)(2,2)
        Assert.Equal(0, field.AdjacentMines(40)); // (4,4) 远处无雷
    }

    // ---------- 翻开与洪水 ----------

    [Fact]
    public void Reveal_ZeroCell_FloodsEntireRegion()
    {
        // 6x6：雷在 (0,2)=2、(1,2)=8、(2,2)=14、(2,1)=13、(2,0)=12 围住左上角
        // (0,0) 是唯一 0 格；洪水应翻开 {(0,0),(0,1),(1,0),(1,1)}，雷格不翻
        var field = new Minefield(6, 6, Layout(6, 6, 2, 8, 14, 13, 12));
        var result = field.Reveal(0);
        Assert.Equal(RevealResult.Revealed, result);

        int[] revealed = { 0, 1, 6, 7 };
        foreach (int i in revealed) Assert.True(field.IsRevealed(i), $"格 {i} 应被洪水翻开");
        foreach (int mine in new[] { 2, 8, 14, 13, 12 }) Assert.False(field.IsRevealed(mine), $"雷格 {mine} 不应被翻开");
        Assert.False(field.IsRevealed(5), "远处格不应被翻开");
        Assert.Equal(revealed.Length, field.RevealedCount);
    }

    [Fact]
    public void Reveal_FlaggedCell_DoesNothing()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        field.CycleMark(1); // 插旗
        Assert.Equal(RevealResult.NoOp, field.Reveal(1));
        Assert.False(field.IsRevealed(1));
        Assert.Equal(1, field.FlagCount);
    }

    [Fact]
    public void Reveal_QuestionCell_Reveals()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        field.CycleMark(1);
        field.CycleMark(1); // 旗 → 问号
        Assert.Equal(RevealResult.Revealed, field.Reveal(1));
        Assert.True(field.IsRevealed(1));
    }

    [Fact]
    public void Reveal_AlreadyRevealed_NoOp()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        field.Reveal(1);
        Assert.Equal(RevealResult.NoOp, field.Reveal(1));
    }

    [Fact]
    public void Reveal_OutOfRange_NoOp()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        Assert.Equal(RevealResult.NoOp, field.Reveal(-1));
        Assert.Equal(RevealResult.NoOp, field.Reveal(81));
    }

    [Fact]
    public void Reveal_Mine_ReturnsMine()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 42));
        Assert.Equal(RevealResult.Mine, field.Reveal(42));
    }

    [Fact]
    public void FloodReveal_ClearsQuestionMarksInsideRegion()
    {
        var field = new Minefield(4, 4, Layout(4, 4, 3, 12));
        field.CycleMark(1); // 旗
        field.CycleMark(1); // 问号（位于 0 区边缘格）
        field.Reveal(5);
        // (0,0)=0 是 0 区：问号被洪水翻开并清除
        Assert.True(field.IsRevealed(1));
        Assert.Equal(CellMark.None, field.GetMark(1));
    }

    // ---------- 标记循环 ----------

    [Fact]
    public void CycleMark_None_Flag_Question_None()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        Assert.Equal(CellMark.Flag, field.CycleMark(1));
        Assert.Equal(1, field.FlagCount);
        Assert.Equal(CellMark.Question, field.CycleMark(1));
        Assert.Equal(0, field.FlagCount);
        Assert.Equal(CellMark.None, field.CycleMark(1));
        Assert.Equal(0, field.FlagCount);
    }

    [Fact]
    public void CycleMark_RevealedCell_NoOp()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        field.Reveal(1);
        Assert.Equal(CellMark.None, field.CycleMark(1));
    }

    [Fact]
    public void CycleMark_OutOfRange_NoOp()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        Assert.Equal(CellMark.None, field.CycleMark(99));
    }

    // ---------- Chord ----------

    [Fact]
    public void Chord_FlagsMatchNumber_RevealsRemainingSafeCells()
    {
        // 6x6：雷 (0,2)=2、(1,2)=8、(2,2)=14、(2,1)=13、(2,0)=12 围住左上角
        // (1,1)=7 数字为 5，5 旗插满后 chord 翻开角落 0 区 {0,1,6}
        var field = new Minefield(6, 6, Layout(6, 6, 2, 8, 14, 13, 12));
        field.Reveal(7); // 翻开 (1,1)
        Assert.Equal(5, field.AdjacentMines(7));
        field.CycleMark(2);
        field.CycleMark(8);
        field.CycleMark(14);
        field.CycleMark(13);
        field.CycleMark(12);
        Assert.Equal(ChordResult.Revealed, field.Chord(7));

        foreach (int i in new[] { 0, 1, 6 }) Assert.True(field.IsRevealed(i), $"格 {i} 应被 chord 翻开");
        foreach (int mine in new[] { 2, 8, 14, 13, 12 }) Assert.False(field.IsRevealed(mine), $"旗格 {mine} 不可被 chord 翻开");
        Assert.Equal(4, field.RevealedCount);
    }

    [Fact]
    public void Chord_FlagsMismatch_NoOp()
    {
        var field = new Minefield(5, 5, Layout(5, 5, 12));
        field.Reveal(6);
        Assert.Equal(ChordResult.NoOp, field.Chord(6)); // 0 旗 ≠ 数字 1
    }

    [Fact]
    public void Chord_WrongFlag_HitsMine()
    {
        // 雷在 (2,2)=12。数字 1 格 (1,1)=6 的旗标在非雷格 (1,2)=11 上，chord 翻开 (2,1)=7 时踩雷
        var field = new Minefield(5, 5, Layout(5, 5, 12));
        field.Reveal(6);
        field.CycleMark(11); // 旗标放错位置 (1,2)
        Assert.Equal(ChordResult.Mine, field.Chord(6));
    }

    [Fact]
    public void Chord_OnHiddenCell_NoOp()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 0));
        Assert.Equal(ChordResult.NoOp, field.Chord(5));
    }

    [Fact]
    public void Chord_OnZeroCell_NoOp()
    {
        // (4,4) 是 0 区，数字 0 无 chord 价值
        var field = new Minefield(5, 5, Layout(5, 5, 0));
        field.Reveal(12);
        Assert.Equal(ChordResult.NoOp, field.Chord(12));
    }

    // ---------- 胜负 ----------

    [Fact]
    public void Win_WhenAllSafeCellsRevealed()
    {
        // 5x5 单雷：翻开所有安全格 → Win
        var field = new Minefield(5, 5, Layout(5, 5, 12));
        Assert.False(field.IsWin);
        for (int i = 0; i < 25; i++)
        {
            if (i == 12) continue;
            var result = field.Reveal(i);
            if (result == RevealResult.NoOp) continue; // 已由洪水翻开
        }
        Assert.True(field.IsWin);
    }

    [Fact]
    public void Reveal_Win_ReturnsWin()
    {
        // 3x3 单雷在 (0,0)：翻开全部安全格时必然出现一次 Win 返回
        var field = new Minefield(3, 3, Layout(3, 3, 0));
        bool sawWin = false;
        for (int i = 1; i < 9; i++)
        {
            if (field.Reveal(i) == RevealResult.Win) sawWin = true;
        }
        Assert.True(sawWin);
        Assert.True(field.IsWin);
    }

    // ---------- 重置 ----------

    [Fact]
    public void Reset_ClearsAllState()
    {
        var field = new Minefield(9, 9, Layout(9, 9, 42));
        field.Reveal(0);
        field.CycleMark(1);
        field.CycleMark(2);
        field.Reset();

        Assert.False(field.MinesGenerated);
        Assert.Equal(0, field.RevealedCount);
        Assert.Equal(0, field.FlagCount);
        Assert.False(field.IsWin);
        for (int i = 0; i < field.TotalCells; i++)
        {
            Assert.False(field.IsRevealed(i));
            Assert.Equal(CellMark.None, field.GetMark(i));
        }
    }

    [Fact]
    public void Constructor_InvalidMineCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Minefield(9, 9, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Minefield(9, 9, 81));
    }
}

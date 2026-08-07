using Xunit;

namespace Minesweeper.Core.Tests;

public class DifficultyTests
{
    [Fact]
    public void Presets_AreCorrect()
    {
        Assert.Equal((9, 9, 10), (DifficultySpec.Beginner.Width, DifficultySpec.Beginner.Height, DifficultySpec.Beginner.Mines));
        Assert.Equal((16, 16, 40), (DifficultySpec.Intermediate.Width, DifficultySpec.Intermediate.Height, DifficultySpec.Intermediate.Mines));
        Assert.Equal((30, 16, 99), (DifficultySpec.Expert.Width, DifficultySpec.Expert.Height, DifficultySpec.Expert.Mines));
    }

    [Theory]
    [InlineData(3, 3, 8)]   // 90% 上限恰好
    [InlineData(3, 3, 9)]   // 超过上限 → 修正为 8
    [InlineData(3, 3, 1)]   // 最小雷
    [InlineData(3, 3, 0)]   // 非法 → 修正为 1
    [InlineData(3, 3, -5)]  // 非法 → 修正为 1
    [InlineData(40, 40, 99999)] // 超大 → 修正为 1440
    [InlineData(1, 1, 1)]   // 宽高过小 → 3x3
    [InlineData(100, 100, 10)] // 宽高过大 → 40x40
    [InlineData(16, 16, 40)] // 标准中级
    public void Normalize_ClampsToValidRange(int w, int h, int m)
    {
        var spec = DifficultySpec.Normalize(w, h, m);
        Assert.InRange(spec.Width, 3, 40);
        Assert.InRange(spec.Height, 3, 40);
        Assert.InRange(spec.Mines, 1, spec.Width * spec.Height - 1);
        Assert.True(spec.Mines <= spec.TotalCells * 0.9);
    }

    [Fact]
    public void From_PresetDifficulties()
    {
        Assert.Equal(DifficultySpec.Beginner, DifficultySpec.From(GameDifficulty.Beginner, 10, 10, 10));
        Assert.Equal(DifficultySpec.Intermediate, DifficultySpec.From(GameDifficulty.Intermediate, 10, 10, 10));
        Assert.Equal(DifficultySpec.Expert, DifficultySpec.From(GameDifficulty.Expert, 10, 10, 10));
    }

    [Fact]
    public void From_Custom_NormalizesValues()
    {
        var spec = DifficultySpec.From(GameDifficulty.Custom, 5, 5, 100);
        Assert.Equal((5, 5, 22), (spec.Width, spec.Height, spec.Mines)); // 25*0.9=22
    }

    [Fact]
    public void SettingsData_SetCustom_StoresNormalized()
    {
        var settings = new SettingsData();
        settings.SetCustom(5, 5, 100);
        Assert.Equal(5, settings.CustomWidth);
        Assert.Equal(5, settings.CustomHeight);
        Assert.Equal(22, settings.CustomMines);
        Assert.Equal(22, settings.Spec.Mines);
    }

    [Fact]
    public void SettingsData_DefaultSpec_IsBeginner()
    {
        var settings = new SettingsData();
        Assert.Equal(DifficultySpec.Beginner, settings.Spec);
    }
}

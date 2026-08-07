using System.Text.Json.Serialization;

namespace Minesweeper.Core;

/// <summary>应用主题</summary>
public enum AppTheme
{
    System = 0, // 跟随系统
    Light = 1,
    Dark = 2,
}

/// <summary>
/// 用户设置数据（纯数据，可序列化）。
/// 持久化读写由 UI 层的 SettingsStore 负责（ApplicationData.LocalSettings）。
/// </summary>
public sealed class SettingsData
{
    public GameDifficulty Difficulty { get; set; } = GameDifficulty.Beginner;
    public int CustomWidth { get; set; } = 16;
    public int CustomHeight { get; set; } = 16;
    public int CustomMines { get; set; } = 40;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public KeyBindingData KeyBindings { get; set; } = new();

    [JsonIgnore]
    public DifficultySpec Spec => DifficultySpec.From(Difficulty, CustomWidth, CustomHeight, CustomMines);

    /// <summary>设置自定义难度（自动规范化非法值，并切换到自定义模式）</summary>
    public void SetCustom(int width, int height, int mines)
    {
        var spec = DifficultySpec.Normalize(width, height, mines);
        CustomWidth = spec.Width;
        CustomHeight = spec.Height;
        CustomMines = spec.Mines;
        Difficulty = GameDifficulty.Custom;
    }
}

namespace Minesweeper.Core;

/// <summary>难度预设</summary>
public enum GameDifficulty
{
    Beginner = 0,      // 9x9  10 雷
    Intermediate = 1,  // 16x16 40 雷
    Expert = 2,        // 30x16 99 雷
    Custom = 3,        // 自定义
}

/// <summary>棋盘尺寸与雷数规格</summary>
public readonly record struct DifficultySpec(int Width, int Height, int Mines)
{
    public static DifficultySpec Beginner => new(9, 9, 10);
    public static DifficultySpec Intermediate => new(16, 16, 40);
    public static DifficultySpec Expert => new(30, 16, 99);

    public int TotalCells => Width * Height;

    /// <summary>按规则修正非法参数：宽高 3~40，雷数 1 ~ min(90%格数, 格数-1)</summary>
    public static DifficultySpec Normalize(int width, int height, int mines)
    {
        width = Math.Clamp(width, 3, 40);
        height = Math.Clamp(height, 3, 40);
        int maxMines = Math.Min((int)(width * height * 0.9), width * height - 1);
        mines = Math.Clamp(mines, 1, maxMines);
        return new DifficultySpec(width, height, mines);
    }

    public static DifficultySpec From(GameDifficulty difficulty, int customWidth, int customHeight, int customMines)
        => difficulty switch
        {
            GameDifficulty.Beginner => Beginner,
            GameDifficulty.Intermediate => Intermediate,
            GameDifficulty.Expert => Expert,
            _ => Normalize(customWidth, customHeight, customMines),
        };
}

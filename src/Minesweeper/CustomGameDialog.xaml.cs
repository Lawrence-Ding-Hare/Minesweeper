using Microsoft.UI.Xaml.Controls;
using Minesweeper.Core;

namespace Minesweeper;

/// <summary>自定义难度对话框（原生 ContentDialog + NumberBox）</summary>
public sealed partial class CustomGameDialog : ContentDialog
{
    public CustomGameDialog()
    {
        InitializeComponent();
        WidthBox.ValueChanged += (_, _) => NormalizeMines();
        HeightBox.ValueChanged += (_, _) => NormalizeMines();
    }

    /// <summary>以当前设置预填输入框</summary>
    public void Prefill(SettingsData settings)
    {
        var spec = settings.Spec;
        WidthBox.Value = spec.Width;
        HeightBox.Value = spec.Height;
        MinesBox.Value = spec.Mines;
        NormalizeMines();
    }

    /// <summary>读取规范化后的自定义规格（含雷数上限修正）</summary>
    public DifficultySpec GetSpec()
    {
        NormalizeMines();
        return DifficultySpec.Normalize(
            (int)WidthBox.Value,
            (int)HeightBox.Value,
            (int)MinesBox.Value);
    }

    /// <summary>宽高变化时把雷数修正到新上限内</summary>
    private void NormalizeMines()
    {
        int maxMines = Math.Min((int)((int)WidthBox.Value * (int)HeightBox.Value * 0.9),
                                (int)WidthBox.Value * (int)HeightBox.Value - 1);
        if ((int)MinesBox.Value > maxMines)
            MinesBox.Value = maxMines;
    }
}

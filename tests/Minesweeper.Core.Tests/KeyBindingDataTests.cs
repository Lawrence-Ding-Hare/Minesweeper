using Xunit;

namespace Minesweeper.Core.Tests;

public class KeyBindingDataTests
{
    [Fact]
    public void Defaults_ContainAllActions()
    {
        var kb = new KeyBindingData();
        Assert.Equal(Enum.GetValues<GameAction>().Length, kb.Bindings.Count);
        foreach (GameAction action in Enum.GetValues<GameAction>())
            Assert.True(kb.Bindings.ContainsKey(action), $"缺少 {action} 的默认键位");
    }

    [Fact]
    public void Defaults_MatchDocumentedKeys()
    {
        var kb = new KeyBindingData();
        // 方向键 + WASD
        Assert.Contains(37, kb.Bindings[GameAction.MoveLeft]); // ←
        Assert.Contains(65, kb.Bindings[GameAction.MoveLeft]); // A
        Assert.Contains(39, kb.Bindings[GameAction.MoveRight]); // →
        Assert.Contains(68, kb.Bindings[GameAction.MoveRight]); // D
        Assert.Contains(38, kb.Bindings[GameAction.MoveUp]); // ↑
        Assert.Contains(87, kb.Bindings[GameAction.MoveUp]); // W
        Assert.Contains(40, kb.Bindings[GameAction.MoveDown]); // ↓
        Assert.Contains(83, kb.Bindings[GameAction.MoveDown]); // S
        // 空格 / 回车
        Assert.Contains(32, kb.Bindings[GameAction.Reveal]);
        Assert.Contains(13, kb.Bindings[GameAction.Reveal]);
        // F / Q / H / R
        Assert.Contains(70, kb.Bindings[GameAction.Flag]);
        Assert.Contains(81, kb.Bindings[GameAction.Question]);
        Assert.Contains(72, kb.Bindings[GameAction.Hint]);
        Assert.Contains(82, kb.Bindings[GameAction.Restart]);
    }

    [Fact]
    public void Clone_IsIndependentCopy()
    {
        var kb = new KeyBindingData();
        var copy = kb.Clone();
        copy.Bindings[GameAction.Flag][0] = 999;

        Assert.Contains(70, kb.Bindings[GameAction.Flag]);
        Assert.DoesNotContain(999, kb.Bindings[GameAction.Flag]);
        Assert.Equal(999, copy.Bindings[GameAction.Flag][0]);
    }

    [Fact]
    public void FindConflict_DetectsKeysUsedByOtherActions()
    {
        var kb = new KeyBindingData();
        // F(70) 已绑定到 Flag
        Assert.Equal(GameAction.Flag, kb.FindConflict(70, GameAction.Reveal));
        // 自身动作不视为冲突
        Assert.Null(kb.FindConflict(70, GameAction.Flag));
        // 未绑定键无冲突
        Assert.Null(kb.FindConflict(200, GameAction.Flag));
    }

    [Fact]
    public void ResetToDefaults_RestoresFactoryBindings()
    {
        var kb = new KeyBindingData();
        kb.Bindings[GameAction.Hint] = new List<int> { 200 };
        kb.ResetToDefaults();
        Assert.Contains(72, kb.Bindings[GameAction.Hint]);
        Assert.DoesNotContain(200, kb.Bindings[GameAction.Hint]);
    }

    [Theory]
    [InlineData(37, "←")]
    [InlineData(32, "空格")]
    [InlineData(13, "回车")]
    [InlineData(70, "F")]
    [InlineData(49, "1")]
    [InlineData(112, "F1")]
    [InlineData(120, "F9")]
    public void KeyName_FormatsCommonKeys(int vk, string expected)
    {
        Assert.Equal(expected, KeyBindingData.KeyName(vk));
    }
}

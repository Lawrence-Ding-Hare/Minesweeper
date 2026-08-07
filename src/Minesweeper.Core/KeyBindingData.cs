namespace Minesweeper.Core;

/// <summary>游戏动作（键位绑定的对象）。枚举值顺序即持久化顺序，追加新动作必须放在末尾。</summary>
public enum GameAction
{
    MoveLeft,
    MoveRight,
    MoveUp,
    MoveDown,
    Reveal,
    Flag,
    Question,
    Hint,
    Restart,
    Undo,
}

/// <summary>
/// 键位绑定数据：每个动作可绑定 1~2 个键。
/// 核心库保持零 UI 依赖，键以 int 编码存储：
///   低 16 位 = Windows 虚拟键码（VirtualKey 枚举值，如 A=65、F=70、Z=90）
///   第 16 位 = Ctrl　第 17 位 = Alt　第 18 位 = Shift
/// 无修饰键的旧绑定数据（纯键码）完全兼容。
/// </summary>
public sealed class KeyBindingData
{
    public Dictionary<GameAction, List<int>> Bindings { get; set; } = BuildDefaults();

    // 修饰键标志位
    public const int CtrlBit = 0x10000;
    public const int AltBit = 0x20000;
    public const int ShiftBit = 0x40000;

    /// <summary>编码一个键位（修饰键可组合）</summary>
    public static int Encode(int vk, bool ctrl = false, bool alt = false, bool shift = false)
        => (vk & 0xFFFF) | (ctrl ? CtrlBit : 0) | (alt ? AltBit : 0) | (shift ? ShiftBit : 0);

    public static int KeyOf(int code) => code & 0xFFFF;
    public static bool HasCtrl(int code) => (code & CtrlBit) != 0;
    public static bool HasAlt(int code) => (code & AltBit) != 0;
    public static bool HasShift(int code) => (code & ShiftBit) != 0;

    /// <summary>默认键位：方向键/WASD 移动、空格/回车翻开、F 旗、Q 疑问、H 提示、R 重开、Ctrl+Z 撤销</summary>
    private static Dictionary<GameAction, List<int>> BuildDefaults() => new()
    {
        [GameAction.MoveLeft] = new() { 37, 65 },   // ← / A
        [GameAction.MoveRight] = new() { 39, 68 },  // → / D
        [GameAction.MoveUp] = new() { 38, 87 },     // ↑ / W
        [GameAction.MoveDown] = new() { 40, 83 },   // ↓ / S
        [GameAction.Reveal] = new() { 32, 13 },     // 空格 / 回车
        [GameAction.Flag] = new() { 70 },           // F
        [GameAction.Question] = new() { 81 },       // Q
        [GameAction.Hint] = new() { 72 },           // H
        [GameAction.Restart] = new() { 82 },        // R
        [GameAction.Undo] = new() { Encode(90, ctrl: true) }, // Ctrl+Z
    };

    /// <summary>重置为默认键位</summary>
    public void ResetToDefaults() => Bindings = BuildDefaults();

    /// <summary>深拷贝（对话框编辑用工作副本）</summary>
    public KeyBindingData Clone() => new()
    {
        Bindings = Bindings.ToDictionary(kv => kv.Key, kv => new List<int>(kv.Value)),
    };

    /// <summary>某个键位是否已被指定动作以外的动作占用；返回占用动作，无冲突返回 null</summary>
    public GameAction? FindConflict(int code, GameAction except)
    {
        foreach (var (action, keys) in Bindings)
        {
            if (action == except) continue;
            if (keys.Contains(code)) return action;
        }
        return null;
    }

    /// <summary>键位编码的显示名（如 90|Ctrl → Ctrl+Z、37 → ←、32 → 空格）</summary>
    public static string KeyName(int code)
    {
        string prefix = (HasCtrl(code) ? "Ctrl+" : "") + (HasAlt(code) ? "Alt+" : "") + (HasShift(code) ? "Shift+" : "");
        int vk = KeyOf(code);
        string key = vk switch
        {
            37 => "←", 38 => "↑", 39 => "→", 40 => "↓",
            32 => "空格", 13 => "回车", 8 => "退格", 9 => "Tab", 27 => "Esc",
            16 => "Shift", 17 => "Ctrl", 18 => "Alt",
            >= 112 and <= 135 => $"F{vk - 111}",
            >= 65 and <= 90 => ((char)vk).ToString(),
            >= 48 and <= 57 => ((char)vk).ToString(),
            96 => "数字键盘0", >= 97 and <= 105 => $"数字键盘{vk - 96}",
            _ => vk.ToString(),
        };
        return prefix + key;
    }

    /// <summary>动作的显示名</summary>
    public static string ActionName(GameAction action) => action switch
    {
        GameAction.MoveLeft => "光标左移",
        GameAction.MoveRight => "光标右移",
        GameAction.MoveUp => "光标上移",
        GameAction.MoveDown => "光标下移",
        GameAction.Reveal => "翻开格子",
        GameAction.Flag => "插旗",
        GameAction.Question => "疑问标记",
        GameAction.Hint => "智能提示",
        GameAction.Restart => "重新开始",
        GameAction.Undo => "撤销",
        _ => action.ToString(),
    };
}

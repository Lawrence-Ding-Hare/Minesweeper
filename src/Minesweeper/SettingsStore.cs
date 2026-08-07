using System.Text.Json;
using Minesweeper.Core;
using Windows.Storage;

namespace Minesweeper;

/// <summary>用户设置持久化（ApplicationData.LocalSettings，JSON 序列化）</summary>
public static class SettingsStore
{
    private const string Key = "Settings_v1";

    public static SettingsData Load()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(Key, out var raw) && raw is string json)
            {
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data is not null)
                {
                    // 旧版/异常数据缺键位配置时补默认
                    data.KeyBindings ??= new KeyBindingData();
                    data.KeyBindings.Bindings ??= new KeyBindingData().Bindings;
                    return data;
                }
            }
        }
        catch
        {
            // 数据损坏时回退默认值
        }
        return new SettingsData();
    }

    public static void Save(SettingsData settings)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[Key] = JsonSerializer.Serialize(settings);
        }
        catch
        {
            // 持久化失败不影响游戏进行
        }
    }
}

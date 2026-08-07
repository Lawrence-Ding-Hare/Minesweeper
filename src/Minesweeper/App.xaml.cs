using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Minesweeper;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        Log("App 构造函数开始");
        InitializeComponent();
        Log("InitializeComponent 完成");
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched 开始");
        _window = new MainWindow();
        Log("MainWindow 创建完成");
        _window.Activate();
        Log("窗口激活完成");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log($"未处理异常: {e.Exception}");
    }

    /// <summary>写启动日志（调试崩溃定位用）</summary>
    internal static void Log(string message)
    {
        try
        {
            string dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "MinesweeperLog");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "startup.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }
}

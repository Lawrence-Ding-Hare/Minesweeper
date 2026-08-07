# 扫雷项目构建指南（2026-08-01 正式版）

## ✅ 免证书方案（绿色版）

**绿色免安装版**：桌面 `MinesweeperPortable` 文件夹（或 MinesweeperPortable.zip，32.5MB）
- **无需安装、无需证书**——解压后直接双击 `Minesweeper.exe` 运行
- 自包含（unpackaged + WindowsAppSDKSelfContained），含全部运行时 DLL
- 任何 Win10 1809+/Win11 电脑可用

**构建命令**（unpackaged）：
```
MSBuild ... /p:WindowsPackageType=None /p:WindowsAppSDKSelfContained=true /p:GenerateAppxPackageOnBuild=false
```
（csproj 的 AppxManifest 已加 `Condition="'$(WindowsPackageType)' != 'None'"`）

## ✅ 正式封包完成

**安装包位置**：桌面 `MinesweeperSetup` 文件夹（或 MinesweeperSetup.zip）
- `Minesweeper.msix`（34.9MB，**自包含**——内嵌 WindowsAppRuntime，任何 Win10 1809+/Win11 电脑可直接安装，无需额外运行时）
- `Minesweeper_TemporaryKey.cer`（签名证书）
- `一键安装.cmd`（双击 → 自动装证书+装包，纯英文提示跨电脑兼容）

**跨电脑安装**：复制整个文件夹（或解压 zip）到目标电脑 → 双击"一键安装.cmd" → 完成。
**要求**：Windows 10 1809+ / Windows 11；若失败请开启开发者模式或以管理员运行。

**构建**：csproj 已启用 `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`（自包含）


## 状态：✅ 应用已构建、安装、运行成功

## 构建方法（本机专用——必须用 VS MSBuild）

```bash
# 一键构建（含 SDK 副本准备）
powershell -ExecutionPolicy Bypass -File scripts\build-msix.ps1
# 安装（装证书 + 安装包）
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
# 或手动构建：
dotnet restore src\Minesweeper\Minesweeper.csproj
MSBuildSDKsPath 无需设置（脚本里用 -p 参数）
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" src\Minesweeper\Minesweeper.csproj /p:Configuration=Release /p:Platform=x64 /p:BundledRuntimeIdentifierGraphFile="C:\Program Files\dotnet\sdk\8.0.423\RuntimeIdentifierGraph.json" /p:NETCoreSdkRuntimeIdentifier=win-x64 /p:NetCoreRoot="C:\Program Files\dotnet" /p:NETCoreSdkVersion=8.0.423 /p:NetCoreTargetingPackRoot="C:\Program Files\dotnet\packs"
```

## 本机 XamlCompiler 故障解决方案（关键！）

**问题**：dotnet build 的 XamlCompiler 全部静默失败/崩溃（微软已知缺陷）
**方案**：
1. **必须用 VS MSBuild**（.NET Framework 版）——进程内 XamlCompiler 需要 CLR 元数据 COM 接口（dotnet build 的 .NET 8 不支持）
2. **SDK 8 副本**（C:\dotnet8sdks）——VS MSBuild 缺 .NET SDK 解析器，用副本 + 占位 SDK 解决
3. **5 个 dotnet 主机参数**（BundledRuntimeIdentifierGraphFile 等）——VS MSBuild 不注入
4. **xbf 需独立打包**：csproj 设置 `<DisableEmbeddedXbf>true</DisableEmbeddedXbf>`（默认嵌入模式在本机失效）
5. **WindowsAppSDK 用 1.7.260224002**（1.6 外部编译器本机静默失败；1.8 拆包不兼容）

## 2026-08-01 第六轮修复（已发布）

1. **帮助按钮图标+文字横排**（原来是 CommandBar 外的 AppBarButton 默认竖排）：改为自定义 Button（水平 StackPanel：FontIcon E897 + TextBlock），MinHeight=48 与左侧按钮对齐

## 2026-08-01 第五轮修复（已发布）

1. **按钮完全贴左**：CommandBar Padding=0 + HorizontalAlignment=Left（像素验证：按钮距窗口左缘 4 逻辑 px）
2. **图标补全**（五个按钮全部有图标）：新游戏=Refresh、提示=Bullets、难度=FontIcon E71B（网格）、主题=FontIcon E790（调色板）、帮助=Help
3. **帮助按钮**：Icon=Help + 垂直居中 + 高 48（与左侧按钮同行等高对齐）

## 2026-08-01 第四轮修复（已发布）

1. **工具栏对齐**：帮助按钮 VerticalAlignment=Center + MinHeight/MaxHeight=48（与 CommandBar 按钮同行等高）；CommandBar Padding=4（按钮贴左）
2. **通透性**：SystemBackdrop 从 MicaBackdrop 改为 **DesktopAcrylicBackdrop**（桌面亚克力毛玻璃，比 Mica 明显更透，色调随系统自动）
3. 棋盘背景透明度再降（Light #26EDEDED / Dark #261F1F1F）

## 2026-08-01 第三轮修复（已发布）

1. **工具栏布局**：新游戏/提示/难度/主题在左，帮助按钮移右侧（Grid 双列布局）
2. **Mica 通透性**：棋盘背景透明度降低（Light #3DEDEDED / Dark #301F1F1F），Mica 透出
3. **圆角**：菜单（MenuFlyoutPresenterStyle CornerRadius=8）+ 自定义难度/帮助对话框（ContentDialog CornerRadius=8）
4. **棋盘消失 bug（恶性）**：根因=布局未完成时 BuildBoard 的 cellSize 缩到最小(14)+窗口尺寸 API 在 200% DPI 下缩放异常。修复：
   - RecalculateLayout 在 ActualWidth=0 时用默认 400/500（不缩到最小）
   - BuildBoard 后 DispatcherQueue 延迟重算
   - **窗口尺寸策略改为"不主动 Resize"**（系统默认尺寸+位置，棋盘随窗口自适应 48px 格子）——避免 MoveAndResize/Resize 在 200% DPI 的双重缩放问题
5. **窗口尺寸**：默认 576x333 逻辑（9x9 棋盘 432px 正常显示）

## 2026-08-01 第二轮优化（已发布）

1. **标题栏一体化**：ExtendsContentIntoTitleBar=true + SetTitleBar(TitleBarArea)，Mica 贯穿标题栏与工具栏；CommandBar 背景透明
2. **窗口尺寸**：SizeWindowToBoard 仅首次（_windowSized 标志），居中放置（DisplayArea.MoveAndResize）；难度切换不再改变窗口
3. **菜单圆角**：RoundedMenuFlyoutPresenterStyle（CornerRadius=8）应用到三个 MenuFlyout
4. **新图标**：渐变背景（靛蓝→紫）+ 4x4 棋盘 + 红色地雷（generate-icons.ps1 已更新）

## 运行时已知问题（已修复）

- **Application.RequestedTheme 设置失败**（0x80131515）→ 改用**根元素 ElementTheme** + GetThemeBrush（按 ActualTheme 查主题字典）
- **AcrylicBrush.BackgroundSource 编译报错** → 已删除该属性（默认 Backdrop）
- **DropDownButton 不能放 CommandBar**（WMC0020）→ 已改为 AppBarButton + 代码 Flyout
- **MenuFlyout 不能写 XAML**（本机 XamlCompiler 崩溃）→ 代码创建（BuildMenus）
- **GameResources.xaml 独立文件**运行时找不到 → **已内联到 App.xaml**（勿改回）
- **棋盘格子 null 崩溃** → RecalculateLayout 判空（BuildBoard 先布局后建格）

## 调试日志

- 位置：`%LOCALAPPDATA%\Packages\Minesweeper.Minefield_*\LocalCache\Local\MinesweeperLog\startup.log`
- App.Log() 调用点：App 构造、MainWindow 各步骤、GameBoardControl 布局异常

## 手动测试清单（用户验收）

- [ ] 鼠标左键翻开（首次点击不炸）
- [ ] 右键循环标记（旗→问号→无）
- [ ] 中键 / 左右同按 chord（数字格快速翻开）
- [ ] 键盘：方向键/WASD 移动、空格翻开、F 旗、Q 问号、H 提示、R 重开
- [ ] 难度切换（初级/中级/高级/自定义）
- [ ] 主题切换（浅色/深色/跟随系统）
- [ ] 提示系统（H 键/工具栏按钮）
- [ ] 胜利/失败状态（表情变化 + InfoBar）
- [ ] 计时器与剩余雷数

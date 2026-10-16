# MiniWeather

Windows 11 桌面天气小组件。使用 WPF、Win32 和 Open-Meteo，无 WebView。默认嵌入桌面层；若系统的 WorkerW 行为不兼容，可用 `--windowed` 启动普通非置顶窗口作为降级模式。

## 运行与构建

解压整个 `MiniWeather` 目录，安装 .NET 10 Desktop Runtime 后运行 `MiniWeather.exe`。首次启动时选择城市；右键小组件或托盘图标可刷新、设置、切换城市和退出。不要只复制 `.exe`，它依赖同目录文件。

在 Windows 上安装 .NET 10 SDK 后，从仓库根目录构建：

```powershell
dotnet build .\MiniWeather.sln -c Release
```

本项目不依赖外部 NuGet 包。GitHub 仓库不包含本机便携式 SDK；下载预编译版本可使用仓库的 Releases 页面。

## 功能与技术取舍

- 显示昨天、今天和未来三天；今天及未来来自 Open-Meteo Forecast API，昨天来自 Historical Weather API 的历史再分析模型，并非气象站实测记录。
- 今天突出显示当前温度，同时保留最高温、最低温和中文天气；当前温度是 Open-Meteo 的天气模型估算值，并非站点实测。旧缓存或请求失败且无该字段时显示 `--°`，不会用最高温冒充当前温度。
- 天气图标为矢量绘制；晴、云、雾、雨、雪、大风、雷暴各有轻微动效。大风由日最大风速判断。系统关闭客户端动画、设置关闭动画或低电量时停止持续动效；持续动效限为 15 FPS。
- 卡片采用 Windows 11 风格的 8px 圆角和轻描边，按天气类型加入柔和底色与强调色；今天的当前温度、高低温分别用颜色区分。

- 背景不透明度、主题、尺寸、城市、刷新间隔、拖动锁定和开机自启可在设置中调整。完全透明背景不会降低文字与图标的不透明度。
- 配置和最近一次成功天气保存在 `%LOCALAPPDATA%\MiniWeather`，以临时文件替换写入；网络失败时展示缓存并标注更新时间。默认每 60 分钟刷新，失败后有上限地退避重试。
- 以 WPF 透明窗口和 Win32 WorkerW 实现桌面层级。WorkerW 是未公开的 Shell 行为：程序会核验嵌入结果，并在窗口失效后重建；无法保证所有 Explorer/虚拟桌面情形。`--windowed` 可明确使用普通窗口，普通窗口不保证 Win+D 后仍显示。
- 位置以物理像素和显示器名称保存；DPI/分辨率变化时检查是否仍在可见区域，避免窗口永久留在屏幕外。应用声明 PerMonitorV2 DPI 感知。

圆角参考 [Microsoft Windows 11 Geometry 指南](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry)；数据字段参考 [Open-Meteo Forecast API](https://open-meteo.com/en/docs)。

开发预览：`MiniWeather.exe --windowed --mock=rain`。`sun`、`cloud`、`fog`、`rain`、`snow`、`wind`、`storm` 均可替换 `rain`。仅在 Mock 模式加 `--force-motion-preview` 可无视本机的系统减少动态效果设置，以检查动画；正常运行始终遵守系统设置。

## 验证记录（2026-10-16，Windows 11 build 26300）

- .NET 10 SDK 10.0.402 Release 构建成功，0 警告、0 错误。
- 实际搜索并选择“济南 · 山东 · 中国”；五天数据及本地缓存成功写入。静态布局在 210×210 和 180×180 逻辑像素下检查，五天均可见；背景设为 0% 后文字和图标仍可见。
- 雨、雪、大风 Mock 预览逐帧检查，三种动效和“大风”描述正常。系统的客户端动画设置为关闭时，正常运行会使用静态图标。
- 实机刷新后今天显示“当前 24°、高 28°、低 17°”；在 180 和 212 逻辑像素尺寸检查了今天的布局，随后恢复原有的 212 尺寸设置。
- 开机自启开关实际测试：关闭会移除注册表启动项，开启会写入正确路径；测试后恢复原有启动项。关闭设置窗口不会退出组件。重复启动仅保留一个进程，无新增错误。
- 正式桌面模式的 20 秒静止采样：CPU 0%，工作集 133.2 MB，私有内存 68.9 MB。强制雨天动效预览在限帧后 15 秒采样：CPU 约为单核的 13.1%，工作集 150.6 MB；限帧前约为单核的 34.8%。这些是短时本机数据，并非长期性能结论。
- 本机只有一块 2560×1440 显示器，因此不同 DPI 的跨屏拖动、拔插无法实测。Explorer 重启及 Win+D 也未做端到端验证；桌面恢复属于尽力实现。托盘退出尚未完成端到端检查。GPU 占用及长期内存稳定性未测量。

## 模块

`WeatherService`、`LocationService`、`WeatherCache`、`SettingsService`、`DesktopWindowHost`、`DisplayPlacement`、`ShellEventHost`、`StartupService`、`TrayService`、`WeatherIcon`、`WidgetView`、`SettingsView` 各自独立。

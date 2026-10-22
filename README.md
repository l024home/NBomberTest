# 简易并发压测工具（WPF + NBomber）

基于 **WPF + NBomber** 的轻量级 HTTP 接口并发压测工具。界面简洁，支持可选的数据文件驱动，压测结束后自动生成 HTML 报告。

## 功能特性

- 图形化界面，填写 URL、每秒请求数、持续时长即可开始压测
- 支持两种模式：
  - **无数据模式**：直接向目标 URL 发送空 Body 的 POST 请求
  - **数据文件模式**：选择 JSON 数组文件，每次请求从文件中循环取一条对象作为 POST 的 JSON Body
- 数据文件使用 `JsonNode` 加载，**无需定义模型类**，任意字段结构的 JSON 对象都能原样发送
- 压测在后台线程执行，界面不会卡死
- 结束后自动生成并打开 HTML 报告，包含 RPS、延迟分位数（p95/p99）、成功率等指标

## 环境要求

| 项目 | 版本 |
| :--- | :--- |
| 操作系统 | Windows（WPF 仅支持 Windows） |
| IDE | Visual Studio 2022（17.8 或更高） |
| .NET | .NET 8.0 (LTS)，目标框架 `net8.0-windows` |
| NBomber | 6.6.0 |
| NBomber.Http | 6.0.1 |
| NBomber.Data | 6.0.0 |

## 项目结构

```
WpfLoadTester/
├── WpfLoadTester.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
└──MainWindow.xaml.cs
```

## 创建项目

1. 在 Visual Studio 中新建 **WPF 应用程序** 项目，目标框架选择 **.NET 8.0**。
2. 通过 NuGet 安装以下包：

```bash
dotnet add package NBomber --version 6.6.0
dotnet add package NBomber.Http --version 6.0.1
dotnet add package NBomber.Data --version 6.0.0
```

或者直接在 `.csproj` 中添加：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NBomber" Version="6.6.0" />
    <PackageReference Include="NBomber.Http" Version="6.0.1" />
    <PackageReference Include="NBomber.Data" Version="6.0.0" />
  </ItemGroup>

</Project>
```

## 界面说明

| 控件 | 说明 |
| :--- | :--- |
| 目标 URL | 要压测的 HTTP 接口地址 |
| 数据文件 | 可选。点击“浏览...”选择 JSON 数组文件；留空则走无数据模式 |
| 每秒请求数 | 负载注入速率（RPS），例如 50 表示每秒发起 50 个请求 |
| 持续（秒） | 压测持续时间 |
| 开始压测 | 启动压测，按钮在运行期间禁用 |
| 状态 | 显示“就绪 / 运行中 / 完成” |

## 数据文件格式

选择数据文件时，JSON 文件**顶层必须是数组**，每个元素是一个对象，代表一组请求 Body 参数：

```json
[
  { "username": "user01", "password": "pass01" },
  { "username": "user02", "password": "pass02" },
  { "username": "user03", "password": "pass03" }
]
```

程序不关心字段名和结构，每个对象会原样序列化为 JSON Body 发送。数据通过 `DataFeed.Circular` 循环使用，保证每条数据都会被轮到。

## 使用步骤

1. 运行程序。
2. 填入目标 URL。
3. （可选）点击“浏览...”选择 JSON 数据文件。
4. 设置每秒请求数和持续秒数。
5. 点击“开始压测”，等待状态变为“压测完成”。
6. 程序会自动在浏览器中打开 `reports` 目录下最新生成的 HTML 报告。

## 压测逻辑说明

- 负载模型使用 `Simulation.Inject`：每秒恒定注入指定数量的请求，持续指定时长。
- HTTP 客户端使用 `Http.CreateDefaultClient()`，它已针对压测场景优化连接池（`MaxConnectionsPerServer = 5000`）和超时策略，避免因连接限制导致数据失真。
- 有数据时走 `POST` + JSON Body；无数据时走空 Body 的 `POST`。若你的接口无数据时需要用 `GET`，把 `else` 分支里的 `"POST"` 改为 `"GET"` 即可。

## 报告解读

HTML 报告重点关注：

- **RPS**：实际每秒处理请求数，是否达到设定速率
- **p95 / p99 延迟**：最慢 5% / 1% 请求的耗时，比平均值更能反映用户体验
- **成功率 / 失败数**：是否有请求报错
- **状态码分布**：确认接口返回是否符合预期

## 注意事项

- **NBomber 授权**：NBomber 对个人使用免费，但在公司/组织内部使用需要商业许可证，团队共用前请确认合规性。
- **本机资源限制**：压测程序本身也消耗 CPU 和网络资源。如果本机资源吃紧，测出的延迟可能偏高，建议在独立机器上运行。
- **数据文件顶层必须是数组**：如果选择了顶层为单个对象的 JSON 文件，加载会失败并弹出错误提示。
- **报告目录**：报告生成在程序运行目录下的 `reports` 文件夹中，每次运行都会新增一份 HTML 文件，程序自动打开最新的一份。

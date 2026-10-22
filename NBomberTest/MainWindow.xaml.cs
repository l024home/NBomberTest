using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Data;
using NBomber.Http.CSharp;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;

namespace NBomberTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? _dataFilePath;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json",
                Title = "选择请求 Body 数据文件（顶层必须是 JSON 数组）"
            };
            if (dialog.ShowDialog() == true)
            {
                _dataFilePath = dialog.FileName;
                TxtDataFile.Text = _dataFilePath;
            }
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            // 1. 读取并校验参数
            string url = TxtUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("请输入目标 URL。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtRate.Text, out int rate) || rate <= 0)
            {
                MessageBox.Show("每秒请求数必须是正整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtDuration.Text, out int durationSeconds) || durationSeconds <= 0)
            {
                MessageBox.Show("持续秒数必须是正整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. 禁用按钮，更新状态
            BtnRun.IsEnabled = false;
            LblStatus.Text = "压测运行中，请稍候...";
            LblStatus.Foreground = System.Windows.Media.Brushes.Blue;

            try
            {
                // 在后台线程执行压测，避免阻塞 UI 线程
                // await 之后会自动回到 UI 线程，无需手动 Dispatcher.Invoke
                await Task.Run(() => RunLoadTest(url, rate, durationSeconds, _dataFilePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"压测过程中发生错误：\n{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnRun.IsEnabled = true;
                LblStatus.Text = "压测完成";
                LblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }

            // 3. 打开最新生成的 HTML 报告
            OpenLatestReport();
        }

        private void RunLoadTest(string url, int rate, int durationSeconds, string? dataFilePath)
        {
            var httpClient = Http.CreateDefaultClient();

            bool isLoadData = !string.IsNullOrWhiteSpace(dataFilePath);
            ScenarioProps scenario;

            if (isLoadData)
            {
                var dataArray = Data.LoadJson<JsonNode[]>(dataFilePath);
                var feed = DataFeed.Circular(dataArray);

                scenario = Scenario.Create("http_api_test", async context =>
                {
                    // 2. 每次请求取一条不同的数据
                    var bodyNode = feed.GetNextItem(context.ScenarioInfo);


                    // 3. 构造带 JSON Body 的 POST 请求
                    var request = Http.CreateRequest("POST", url)
                        .WithJsonBody(bodyNode);

                    var response = await Http.Send(httpClient, request);
                    return response;
                });
            }
            else
            {
                scenario = Scenario.Create("http_api_test", async context =>
                {
                    var request = Http.CreateRequest("POST", url);
                    var response = await Http.Send(httpClient, request);
                    return response;
                });
            }

            scenario = scenario.WithLoadSimulations(
                Simulation.Inject(
                    rate: rate,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(durationSeconds))
            );

            NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder("reports")
                .WithReportFormats(ReportFormat.Html)
                .Run();
        }

        private void OpenLatestReport()
        {
            var reportDir = Path.GetFullPath("reports");
            if (!Directory.Exists(reportDir)) return;

            var latestHtml = Directory.GetFiles(reportDir, "*.html")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();

            if (latestHtml != null)
            {
                Process.Start(new ProcessStartInfo(latestHtml) { UseShellExecute = true });
            }
        }
    }
}
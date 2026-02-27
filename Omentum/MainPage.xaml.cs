using System;
using System.Threading;
using System.Threading.Tasks;

namespace Omentum
{
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource _cancellationTokenSource;
        private const int UpdateInterval = 2000; // 2秒更新一次

        public MainPage()
        {
            InitializeComponent();
            InitializeEventHandlers();
        }

        private void InitializeEventHandlers()
        {
            // 性能模式选择
            PerformanceModePicker.SelectedIndexChanged += async (s, e) =>
            {
                if (PerformanceModePicker.SelectedIndex >= 0)
                {
                    await ApplyPerformanceMode(PerformanceModePicker.SelectedItem.ToString());
                }
            };

            // 风扇模式选择
            FanModePicker.SelectedIndexChanged += async (s, e) =>
            {
                if (FanModePicker.SelectedIndex >= 0)
                {
                    await ApplyFanMode(FanModePicker.SelectedItem.ToString());
                }
            };

            // 风扇转速滑块
            FanSpeedSlider.ValueChanged += async (s, e) =>
            {
                await ApplyFanSpeed((int)e.NewValue);
            };

            // CPU 控制选择
            CpuControlPicker.SelectedIndexChanged += async (s, e) =>
            {
                if (CpuControlPicker.SelectedIndex >= 0)
                {
                    await ApplyCpuControl(CpuControlPicker.SelectedItem.ToString());
                }
            };

            // GPU 控制选择
            GpuControlPicker.SelectedIndexChanged += async (s, e) =>
            {
                if (GpuControlPicker.SelectedIndex >= 0)
                {
                    await ApplyGpuControl(GpuControlPicker.SelectedItem.ToString());
                }
            };
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartPerformanceMonitoring();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopPerformanceMonitoring();
        }

        private void StartPerformanceMonitoring()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => UpdatePerformanceMetricsLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        private void StopPerformanceMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        private async Task UpdatePerformanceMetricsLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var metrics = HardwareMonitor.GetPerformanceMetrics();
                    await MainThread.InvokeOnMainThreadAsync(() => UpdateUI(metrics));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating performance metrics: {ex.Message}");
                }

                await Task.Delay(UpdateInterval, cancellationToken);
            }
        }

        private void UpdateUI(PerformanceMetrics metrics)
        {
            // CPU 信息
            CpuFrequencyLabel.Text = $"{metrics.CpuFrequency:F2} GHz";
            CpuPowerLabel.Text = $"{metrics.CpuPower:F1} W";
            CpuTemperatureLabel.Text = $"{metrics.CpuTemperature:F1}°C";

            // GPU 信息
            GpuFrequencyLabel.Text = $"{metrics.GpuFrequency:F0} MHz";
            GpuPowerLabel.Text = $"{metrics.GpuPower:F1} W";
            GpuTemperatureLabel.Text = $"{metrics.GpuTemperature:F1}°C";

            // 内存信息
            MemoryLabel.Text = $"{metrics.MemoryUsed / 1024:F1} GB / {metrics.MemoryTotal / 1024:F1} GB";

            // 网络信息
            NetworkLabel.Text = $"{metrics.NetworkDownload:F2} MB/s / {metrics.NetworkUpload:F2} MB/s";

            // 电池信息
            BatteryLabel.Text = $"{metrics.BatteryPercent:F0}% / {metrics.BatteryStatus}";
            PowerStatusLabel.Text = $"电源状态: {(metrics.IsPowerConnected ? "已连接" : "未连接")}";

            // 风扇信息
            FansLabel.Text = $"{metrics.FanLeftSpeed} RPM / {metrics.FanRightSpeed} RPM";

            // 更新风扇滑块显示
            UpdateFanSpeedUI(metrics.FanLeftSpeed, metrics.FanRightSpeed);
        }

        private void UpdateFanSpeedUI(int leftSpeed, int rightSpeed)
        {
            int avgSpeed = (leftSpeed + rightSpeed) / 2;
            FanSpeedCurrentLabel.Text = $"{avgSpeed} RPM";
            FanSpeedSlider.Value = avgSpeed * 100.0 / 6400; // 6400 是最大转速
        }

        private async Task ApplyPerformanceMode(string mode)
        {
            try
            {
                switch (mode)
                {
                    case "高性能模式":
                        HardwareMonitor.SetFanMode(true);
                        HardwareMonitor.SetCpuPowerLimit(80);
                        break;
                    case "省电模式":
                        HardwareMonitor.SetFanMode(false);
                        HardwareMonitor.SetCpuPowerLimit(30);
                        break;
                    case "平衡模式":
                    default:
                        HardwareMonitor.SetFanMode(false);
                        HardwareMonitor.SetCpuPowerLimit(50);
                        break;
                }
                await DisplayAlert("提示", $"已切换到{mode}", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"切换性能模式失败: {ex.Message}", "确定");
            }
        }

        private async Task ApplyFanMode(string mode)
        {
            try
            {
                bool performanceMode = mode == "性能模式";
                HardwareMonitor.SetFanMode(performanceMode);
                await DisplayAlert("提示", $"已切换到{mode}", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"切换风扇模式失败: {ex.Message}", "确定");
            }
        }

        private async Task ApplyFanSpeed(int percentage)
        {
            try
            {
                int rpm = (int)(percentage * 64); // 0-100% 对应 0-6400 RPM
                HardwareMonitor.SetFanSpeed(rpm, rpm);
                FanSpeedCurrentLabel.Text = $"{rpm} RPM";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting fan speed: {ex.Message}");
            }
        }

        private async Task ApplyCpuControl(string control)
        {
            try
            {
                byte powerLimit = control switch
                {
                    "最大性能" => 80,
                    "最大节能" => 30,
                    "自动" => 50,
                    _ => 50
                };

                HardwareMonitor.SetCpuPowerLimit(powerLimit);
                await DisplayAlert("提示", $"已设置CPU控制为{control}", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"设置CPU控制失败: {ex.Message}", "确定");
            }
        }

        private async Task ApplyGpuControl(string control)
        {
            try
            {
                // GPU 控制功能（根据实际硬件实现）
                await DisplayAlert("提示", $"已设置GPU控制为{control}", "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"设置GPU控制失败: {ex.Message}", "确定");
            }
        }
    }
}

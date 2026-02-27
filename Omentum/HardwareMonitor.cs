using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Omentum
{
    public class PerformanceMetrics
    {
        public double CpuFrequency { get; set; }
        public double CpuPower { get; set; }
        public double CpuTemperature { get; set; }
        public double GpuFrequency { get; set; }
        public double GpuPower { get; set; }
        public double GpuTemperature { get; set; }
        public double MemoryUsed { get; set; }
        public double MemoryTotal { get; set; }
        public double NetworkDownload { get; set; }
        public double NetworkUpload { get; set; }
        public double BatteryPercent { get; set; }
        public string BatteryStatus { get; set; } = "未知";
        public bool IsPowerConnected { get; set; }
        public int FanLeftSpeed { get; set; }
        public int FanRightSpeed { get; set; }
    }

    public class HardwareMonitor
    {
#if WINDOWS
        private static readonly PerformanceCounterWrapper? _cpuCounter = new PerformanceCounterWrapper("Processor", "% Processor Time", "_Total");
        private static readonly PerformanceCounterWrapper? _memoryCounter = new PerformanceCounterWrapper("Memory", "Available MBytes");
#endif
        private static Dictionary<string, NetworkInfo> _networkInterfaces = new Dictionary<string, NetworkInfo>();
        private static DateTime _lastNetworkCheck = DateTime.Now;

        public static PerformanceMetrics GetPerformanceMetrics()
        {
            var metrics = new PerformanceMetrics();

            try
            {
                // CPU 信息
                GetCpuInfo(metrics);

                // GPU 信息
                GetGpuInfo(metrics);

                // 内存信息
                GetMemoryInfo(metrics);

                // 网络信息
                GetNetworkInfo(metrics);

                // 电池信息
                GetBatteryInfo(metrics);

                // 风扇信息（仅支持 HP Omen 设备）
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    GetFanInfo(metrics);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting performance metrics: {ex.Message}");
            }

            return metrics;
        }

        private static void GetCpuInfo(PerformanceMetrics metrics)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed, LoadPercentage FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        metrics.CpuFrequency = Convert.ToDouble(obj["CurrentClockSpeed"]) / 1000; // MHz to GHz
                        break;
                    }
                }

                // CPU 温度
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        metrics.CpuTemperature = (Convert.ToDouble(obj["CurrentTemperature"]) - 2732) / 10;
                        break;
                    }
                }
            }
            catch
            {
                // 如果无法获取信息，保持默认值
            }
        }

        private static void GetGpuInfo(PerformanceMetrics metrics)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT AdapterRAM, CurrentClockSpeed FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        metrics.GpuFrequency = Convert.ToDouble(obj["CurrentClockSpeed"]) / 1000; // MHz to GHz
                        break;
                    }
                }

                // 尝试从 NVIDIA 或 AMD 获取 GPU 温度
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT CurrentTemperature FROM Win32_VideoController"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            if (obj["CurrentTemperature"] != null)
                            {
                                metrics.GpuTemperature = Convert.ToDouble(obj["CurrentTemperature"]);
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
            catch
            {
                // 如果无法获取信息，保持默认值
            }
        }

        private static void GetMemoryInfo(PerformanceMetrics metrics)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        metrics.MemoryTotal = Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024; // KB to MB
                        break;
                    }
                }

                double availableMemory;
#if WINDOWS
                if (_memoryCounter != null)
                {
                    availableMemory = _memoryCounter.NextValue();
                }
                else
                {
#endif
                    // 使用 WMI 获取可用内存（适用于非 Windows 平台或 PerformanceCounter 不可用时）
                    using (var memorySearcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
                    {
                        foreach (ManagementObject obj in memorySearcher.Get())
                        {
                            availableMemory = Convert.ToDouble(obj["FreePhysicalMemory"]);
                            break;
                        }
                        availableMemory = 0; // 默认值
                    }
#if WINDOWS
                }
#endif
                metrics.MemoryUsed = metrics.MemoryTotal - availableMemory;
            }
            catch
            {
                // 如果无法获取信息，保持默认值
            }
        }

        private static void GetNetworkInfo(PerformanceMetrics metrics)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, BytesReceivedPersec, BytesSentPersec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface"))
                {
                    var currentStats = new Dictionary<string, NetworkInfo>();
                    DateTime now = DateTime.Now;

                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"].ToString();
                        long bytesReceived = Convert.ToInt64(obj["BytesReceivedPersec"]);
                        long bytesSent = Convert.ToInt64(obj["BytesSentPersec"]);

                        currentStats[name] = new NetworkInfo
                        {
                            BytesReceived = bytesReceived,
                            BytesSent = bytesSent
                        };
                    }

                    if (_networkInterfaces.Count > 0)
                    {
                        double totalDownload = 0;
                        double totalUpload = 0;

                        foreach (var kvp in currentStats)
                        {
                            if (_networkInterfaces.ContainsKey(kvp.Key))
                            {
                                var prev = _networkInterfaces[kvp.Key];
                                double timeElapsed = (now - _lastNetworkCheck).TotalSeconds;

                                if (timeElapsed > 0)
                                {
                                    totalDownload += (kvp.Value.BytesReceived - prev.BytesReceived) / timeElapsed;
                                    totalUpload += (kvp.Value.BytesSent - prev.BytesSent) / timeElapsed;
                                }
                            }
                        }

                        metrics.NetworkDownload = totalDownload / 1024 / 1024; // Bytes to MB/s
                        metrics.NetworkUpload = totalUpload / 1024 / 1024;
                    }

                    _networkInterfaces = currentStats;
                    _lastNetworkCheck = now;
                }
            }
            catch
            {
                // 如果无法获取信息，保持默认值
            }
        }

        private static void GetBatteryInfo(PerformanceMetrics metrics)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining, BatteryStatus, Status FROM Win32_Battery"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        metrics.BatteryPercent = Convert.ToDouble(obj["EstimatedChargeRemaining"]);
                        
                        int status = Convert.ToInt32(obj["BatteryStatus"]);
                        metrics.BatteryStatus = status switch
                        {
                            1 => "放电中",
                            2 => "已充满",
                            3 => "充电中",
                            4 => "低电量",
                            5 => "警告",
                            6 => "未知",
                            7 => "完全充电",
                            _ => "未知"
                        };

                        // 判断是否接通电源：充电中、已充满、完全充电状态表示接通电源
                        metrics.IsPowerConnected = status is 2 or 3 or 7;
                        break;
                    }
                }
            }
            catch
            {
                metrics.BatteryStatus = "未检测到电池";
                metrics.IsPowerConnected = false;
            }
        }

        private static void GetFanInfo(PerformanceMetrics metrics)
        {
            try
            {
                // 使用 HP Omen WMI 获取风扇信息
                const string namespaceName = @"root\wmi";
                const string className = "hpqBIntM";
                byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

                using (var biosDataIn = new ManagementClass(namespaceName, "hpqBDataIn", null).CreateInstance())
                {
                    biosDataIn["Command"] = 0x20008;
                    biosDataIn["CommandType"] = 0x2D;
                    biosDataIn["Sign"] = sign;
                    biosDataIn["Size"] = 4;

                    using (var searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}"))
                    {
                        var biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                        if (biosMethods != null)
                        {
                            var inParams = biosMethods.GetMethodParameters("hpqBIOSInt128");
                            inParams["InData"] = biosDataIn;

                            var result = biosMethods.InvokeMethod("hpqBIOSInt128", inParams, null);
                            var outData = result["OutData"] as ManagementBaseObject;
                            uint returnCode = (uint)outData["rwReturnCode"];

                            if (returnCode == 0)
                            {
                                var outputData = (byte[])outData["Data"];
                                if (outputData != null && outputData.Length >= 2)
                                {
                                    metrics.FanLeftSpeed = outputData[0] * 100;
                                    metrics.FanRightSpeed = outputData[1] * 100;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果不是 HP Omen 设备或无法获取风扇信息，保持默认值
            }
        }

        public static void SetFanMode(bool performanceMode)
        {
            try
            {
                const string namespaceName = @"root\wmi";
                const string className = "hpqBIntM";
                byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

                byte mode = performanceMode ? (byte)0x31 : (byte)0x30;

                using (var biosDataIn = new ManagementClass(namespaceName, "hpqBDataIn", null).CreateInstance())
                {
                    biosDataIn["Command"] = 0x20008;
                    biosDataIn["CommandType"] = 0x1A;
                    biosDataIn["Sign"] = sign;
                    biosDataIn["hpqBData"] = new byte[] { 0xFF, mode };
                    biosDataIn["Size"] = 2;

                    using (var searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}"))
                    {
                        var biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                        if (biosMethods != null)
                        {
                            var inParams = biosMethods.GetMethodParameters("hpqBIOSInt0");
                            inParams["InData"] = biosDataIn;

                            biosMethods.InvokeMethod("hpqBIOSInt0", inParams, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting fan mode: {ex.Message}");
            }
        }

        public static void SetFanSpeed(int leftSpeed, int rightSpeed)
        {
            try
            {
                const string namespaceName = @"root\wmi";
                const string className = "hpqBIntM";
                byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

                using (var biosDataIn = new ManagementClass(namespaceName, "hpqBDataIn", null).CreateInstance())
                {
                    biosDataIn["Command"] = 0x20008;
                    biosDataIn["CommandType"] = 0x2E;
                    biosDataIn["Sign"] = sign;
                    biosDataIn["hpqBData"] = new byte[] { (byte)(leftSpeed / 100), (byte)(rightSpeed / 100) };
                    biosDataIn["Size"] = 2;

                    using (var searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}"))
                    {
                        var biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                        if (biosMethods != null)
                        {
                            var inParams = biosMethods.GetMethodParameters("hpqBIOSInt0");
                            inParams["InData"] = biosDataIn;

                            biosMethods.InvokeMethod("hpqBIOSInt0", inParams, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting fan speed: {ex.Message}");
            }
        }

        public static void SetCpuPowerLimit(byte powerLimit)
        {
            try
            {
                const string namespaceName = @"root\wmi";
                const string className = "hpqBIntM";
                byte[] sign = { 0x53, 0x45, 0x43, 0x55 };

                using (var biosDataIn = new ManagementClass(namespaceName, "hpqBDataIn", null).CreateInstance())
                {
                    biosDataIn["Command"] = 0x20008;
                    biosDataIn["CommandType"] = 0x29;
                    biosDataIn["Sign"] = sign;
                    biosDataIn["hpqBData"] = new byte[] { powerLimit, powerLimit, 0xFF, 0xFF };
                    biosDataIn["Size"] = 4;

                    using (var searcher = new ManagementObjectSearcher(namespaceName, $"SELECT * FROM {className}"))
                    {
                        var biosMethods = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                        if (biosMethods != null)
                        {
                            var inParams = biosMethods.GetMethodParameters("hpqBIOSInt0");
                            inParams["InData"] = biosDataIn;

                            biosMethods.InvokeMethod("hpqBIOSInt0", inParams, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting CPU power limit: {ex.Message}");
            }
        }

        private class NetworkInfo
        {
            public long BytesReceived { get; set; }
            public long BytesSent { get; set; }
        }

#if WINDOWS
        private class PerformanceCounterWrapper
        {
            private System.Diagnostics.PerformanceCounter? _counter;

            public PerformanceCounterWrapper(string category, string counter, string? instance = null)
            {
                try
                {
                    _counter = new System.Diagnostics.PerformanceCounter(category, counter, instance);
                    _ = _counter.NextValue(); // 初始化
                }
                catch
                {
                    _counter = null;
                }
            }

            public float NextValue()
            {
                return _counter?.NextValue() ?? 0;
            }
        }
#endif
    }
}
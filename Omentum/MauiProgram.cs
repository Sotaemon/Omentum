using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Omentum
{
    public static class MauiProgram
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static float CPUTemp = 50, GPUTemp = 40, CPUPower = 0, GPUPower = 0;
        static int DBVersion = 2, countDB = 0, countDBInit = 5, tryTimes = 0, CPULimitDB = 25;
        static int countRestore = 0, gpuClock = 0;
        static int alreadyRead = 0, alreadyReadCode = 1000;
        static string fanTable = "silent", fanMode = "performance", fanControl = "auto", tempSensitivity = "high", cpuPower = "max", gpuPower = "max", autoStart = "off", customIcon = "original", floatingBar = "off", floatingBarLoc = "left", omenKey = "default";

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("SourceHanSerifSC-Regular.otf", "SerifSource");
                });
#if DEBUG
    		builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}

using Microsoft.Extensions.Logging;

namespace Omentum
{
    public static class MauiProgram
    {
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

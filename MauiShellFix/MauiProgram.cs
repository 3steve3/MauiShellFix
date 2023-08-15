using MauiShellFix.Views;
using Microsoft.Extensions.Logging;

namespace MauiShellFix
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
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<Page2>();
#if IOS
		builder.ConfigureMauiHandlers(handlers =>
		{
			handlers.AddHandler<Shell, ShellWorkarounds>();
		});
#endif
            
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
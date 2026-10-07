using GroupAnnouncementApp.Repositories;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services;
using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;
using GroupAnnouncementApp.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Firestore;
using Android.SE.Omapi;


#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#endif

namespace GroupAnnouncementApp;

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

        RegisterFirebaseServices(builder);
        RegisterAppServices(builder.Services);
        RegisterViewModels(builder.Services);
        RegisterViews(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterFirebaseServices(MauiAppBuilder builder)
    {
#if ANDROID
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddAndroid(android =>
                android.OnCreate((activity, _) =>
                    CrossFirebase.Initialize(
                        activity,
                        () => Platform.CurrentActivity!)));
        });
#endif
        builder.Services.AddSingleton(_ => CrossFirebaseAuth.Current);
        builder.Services.AddSingleton(_ => CrossFirebaseFirestore.Current);
    }

    private static void RegisterAppServices(IServiceCollection services)
    {
        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<DashboardViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<DashboardPage>();
    }
}

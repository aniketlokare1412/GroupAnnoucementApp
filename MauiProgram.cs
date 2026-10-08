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
                fonts.AddFont("MaterialIconsRound-Regular.otf", "MaterialIconsRound");
            });

#if ANDROID
        // Plain fields: remove the native underline under Entry and Editor.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
        {
            handler.PlatformView.Background = null;
        });
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
        {
            handler.PlatformView.Background = null;
        });
#endif

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
        services.AddSingleton<IGroupRepository, GroupRepository>();
        services.AddSingleton<IMembershipRepository, MembershipRepository>();
        services.AddSingleton<IAnnouncementRepository, AnnouncementRepository>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IUserAdminService, UserAdminService>();
        services.AddSingleton<IGroupAdminService, GroupAdminService>();
        services.AddSingleton<IMembershipService, MembershipService>();
        services.AddSingleton<IMembershipAdminService, MembershipAdminService>();
        services.AddSingleton<IAnnouncementService, AnnouncementService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<WelcomeViewModel>();
        services.AddTransient<SplashViewModel>();
        services.AddTransient<CompleteProfileViewModel>();
        services.AddTransient<AccountViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<GroupsViewModel>();
        services.AddTransient<GroupEditViewModel>();
        services.AddTransient<GroupMembersViewModel>();
        services.AddTransient<AddMembersViewModel>();
        services.AddTransient<JoinGroupsViewModel>();
        services.AddTransient<AnnouncementGroupsViewModel>();
        services.AddTransient<AnnouncementsViewModel>();
        services.AddTransient<AnnouncementEditViewModel>();
    }

    private static void RegisterViews(IServiceCollection services)
    {
        services.AddTransient<AppShell>();
        services.AddTransient<WelcomePage>();
        services.AddTransient<SplashPage>();
        services.AddTransient<CompleteProfilePage>();
        services.AddTransient<AccountPage>();
        services.AddTransient<HomePage>();
        services.AddTransient<UsersPage>();
        services.AddTransient<GroupsPage>();
        services.AddTransient<GroupEditPage>();
        services.AddTransient<GroupMembersPage>();
        services.AddTransient<AddMembersPage>();
        services.AddTransient<JoinGroupsPage>();
        services.AddTransient<AnnouncementGroupsPage>();
        services.AddTransient<AnnouncementsPage>();
        services.AddTransient<AnnouncementEditPage>();
    }
}

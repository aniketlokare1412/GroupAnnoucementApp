using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Views;

namespace GroupAnnouncementApp;

public partial class AppShell : Shell
{
    // Runs once per app process (AppShell itself can be created more than once,
    // and registering the same route twice must be avoided).
    static AppShell()
    {
        Routing.RegisterRoute(Routes.Users, typeof(UsersPage));
    }

    // The first ShellContent is the page Shell shows at startup: the Splash page.
    // Splash checks the session, loads the profile and then navigates to the right screen.
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        Items.Add(CreateContent("splash", typeof(SplashPage)));
        Items.Add(CreateContent("login", typeof(LoginPage)));
        Items.Add(CreateContent("register", typeof(RegisterPage)));
        Items.Add(CreateContent("completeprofile", typeof(CompleteProfilePage)));
        Items.Add(CreateContent("dashboard", typeof(DashboardPage)));
    }

    private static ShellContent CreateContent(string route, Type pageType)
    {
        ShellContent content = new ShellContent();
        content.Route = route;
        content.ContentTemplate = new DataTemplate(pageType);
        return content;
    }
}

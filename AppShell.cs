using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.Views;

namespace GroupAnnouncementApp;

public partial class AppShell : Shell
{
    // The first ShellContent added is the page Shell shows at startup.
    // So the session check is simply: signed in -> Dashboard first, else Login first.
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

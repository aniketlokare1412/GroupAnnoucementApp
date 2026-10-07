using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.Views;

namespace GroupAnnouncementApp;

public partial class AppShell : Shell
{
    // The first ShellContent added is the page Shell shows at startup.
    // So the session check is simply: signed in -> Dashboard first, else Login first.
    public AppShell(IAuthService authService)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        ShellContent loginContent = new ShellContent
        {
            Route = "login",
            ContentTemplate = new DataTemplate(typeof(LoginPage))
        };

        ShellContent registerContent = new ShellContent
        {
            Route = "register",
            ContentTemplate = new DataTemplate(typeof(RegisterPage))
        };

        ShellContent dashboardContent = new ShellContent
        {
            Route = "dashboard",
            ContentTemplate = new DataTemplate(typeof(DashboardPage))
        };

        if (authService.IsAuthenticated)
        {
            Items.Add(dashboardContent);
            Items.Add(loginContent);
            Items.Add(registerContent);
        }
        else
        {
            Items.Add(loginContent);
            Items.Add(registerContent);
            Items.Add(dashboardContent);
        }
    }
}

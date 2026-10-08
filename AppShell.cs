using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Views;

namespace GroupAnnouncementApp;

public partial class AppShell : Shell
{
    // Runs once per app process (AppShell itself can be created more than once,
    // and registering the same route twice must be avoided).
    // Only pages that are PUSHED on top of a tab are registered here.
    // The tab pages (Home, Groups, Users, Discover) are declared in the constructor below.
    static AppShell()
    {
        Routing.RegisterRoute(Routes.Account, typeof(AccountPage));
        Routing.RegisterRoute(Routes.GroupEdit, typeof(GroupEditPage));
        Routing.RegisterRoute(Routes.GroupMembers, typeof(GroupMembersPage));
        Routing.RegisterRoute(Routes.AddMembers, typeof(AddMembersPage));
        Routing.RegisterRoute(Routes.AnnouncementGroups, typeof(AnnouncementGroupsPage));
        Routing.RegisterRoute(Routes.Announcements, typeof(AnnouncementsPage));
        Routing.RegisterRoute(Routes.AnnouncementEdit, typeof(AnnouncementEditPage));
    }

    // The first item is the page Shell shows at startup: the Splash page.
    // Splash checks the session, loads the profile and then navigates to the right screen:
    // Welcome (signed out), Complete profile, or the admin / member tab bar.
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        Items.Add(CreateContent("splash", null, null, typeof(SplashPage)));
        Items.Add(CreateContent("login", null, null, typeof(WelcomePage)));
        Items.Add(CreateContent("completeprofile", null, null, typeof(CompleteProfilePage)));

        // Admin tab bar: Home, Groups, Users. Tab routes are unique across both bars on purpose.
        Items.Add(CreateTabBar(
            "adminshell",
            CreateContent("adminhome", "Home", "tab_home.png", typeof(HomePage)),
            CreateContent("admingroups", "Groups", "tab_groups.png", typeof(GroupsPage)),
            CreateContent("adminusers", "Users", "tab_users.png", typeof(UsersPage))));

        // Member (regular user) tab bar: Home, Discover.
        Items.Add(CreateTabBar(
            "membershell",
            CreateContent("memberhome", "Home", "tab_home.png", typeof(HomePage)),
            CreateContent("memberdiscover", "Discover", "tab_discover.png", typeof(JoinGroupsPage))));
    }

    private static ShellContent CreateContent(string route, string? title, string? icon, Type pageType)
    {
        ShellContent content = new ShellContent();
        content.Route = route;
        content.ContentTemplate = new DataTemplate(pageType);

        if (title != null)
        {
            content.Title = title;
        }

        if (icon != null)
        {
            content.Icon = icon;
        }

        return content;
    }

    private static TabBar CreateTabBar(string route, params ShellContent[] tabs)
    {
        TabBar bar = new TabBar();
        bar.Route = route;

        for (int i = 0; i < tabs.Length; i++)
        {
            bar.Items.Add(tabs[i]);
        }

        return bar;
    }
}

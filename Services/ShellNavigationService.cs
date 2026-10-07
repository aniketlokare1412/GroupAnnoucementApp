using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);
}

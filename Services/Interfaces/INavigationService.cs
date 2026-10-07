namespace GroupAnnouncementApp.Services.Interfaces;

public interface INavigationService
{
    Task GoToAsync(string route);
}
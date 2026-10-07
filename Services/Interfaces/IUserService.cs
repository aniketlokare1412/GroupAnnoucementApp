using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.Services.Interfaces;

public interface IUserService
{
    // Creates the Auth account and the users/{uid} profile (always a Regular user).
    Task<OperationResult> RegisterAsync(string name, string phone, string email, string password);
}

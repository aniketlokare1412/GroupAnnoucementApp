using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

public interface IUserService
{
    // Creates the Auth account and the users/{uid} profile (always a Regular user).
    Task<OperationResult> RegisterAsync(string name, string phone, string email, string password);

    // IsSuccess + Value == null  -> the profile document does not exist.
    // IsSuccess + Value != null  -> profile found.
    // !IsSuccess                 -> could not load (offline, timeout, error).
    Task<OperationResult<UserProfile?>> GetProfileAsync(string uid);

    // Creates users/{uid} for the signed-in user who has no profile yet (always Regular).
    // Never deletes the Auth account if it fails.
    Task<OperationResult> CreateOwnProfileAsync(string name, string phone);
}

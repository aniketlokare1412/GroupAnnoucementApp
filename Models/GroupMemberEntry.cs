namespace GroupAnnouncementApp.Models;

// One row of the admin's member list: the membership joined with the user's profile.
public sealed class GroupMemberEntry
{
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    // True when no users/{uid} profile exists any more.
    public bool ProfileMissing { get; set; }

    public bool IsUserActive { get; set; }

    public bool IsAdmin { get; set; }
}

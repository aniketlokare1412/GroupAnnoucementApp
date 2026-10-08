namespace GroupAnnouncementApp.Helpers;

public static class Routes
{
    // ---- Full-screen pages (no tab bar) ----
    public const string Splash = "//splash";

    // The Welcome page (sign in + create account in one page). The route name is kept as "login".
    public const string Login = "//login";

    public const string CompleteProfile = "//completeprofile";

    // ---- Admin tab bar: Home, Groups, Users ----
    public const string AdminHome = "//adminshell/adminhome";
    public const string AdminGroups = "//adminshell/admingroups";
    public const string AdminUsers = "//adminshell/adminusers";

    // ---- Member tab bar: Home, Discover ----
    public const string MemberHome = "//membershell/memberhome";
    public const string MemberDiscover = "//membershell/memberdiscover";

    // ---- Pages pushed on top of a tab ----
    // Account (profile + two-step log out). Opened from the avatar at the top of Home.
    public const string Account = "account";

    // Used for both "new group" and "edit group" (groupedit?groupId=...).
    public const string GroupEdit = "groupedit";

    // Admin: members of one group (groupmembers?groupId=...) and the add-members picker.
    public const string GroupMembers = "groupmembers";

    public const string AddMembers = "addmembers";

    // Admin: pick a group, then see/manage its announcements. (No longer reachable from the UI;
    // the admin Groups tab opens a group's announcements directly. Removed in a later phase.)
    public const string AnnouncementGroups = "announcementgroups";

    // One group's announcements (announcements?groupId=...&groupName=...). Admins and members.
    public const string Announcements = "announcements";

    // Admin: new / edit announcement. No groupId = pick one or more groups.
    // announcementedit?groupId=..&groupName=..  = new for that group
    // announcementedit?groupId=..&groupName=..&announcementId=..  = edit
    public const string AnnouncementEdit = "announcementedit";
}

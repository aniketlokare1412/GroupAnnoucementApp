namespace GroupAnnouncementApp.Helpers;

public static class Routes
{
    public const string Splash = "//splash";

    public const string Login = "//login";

    public const string Register = "//register";

    public const string CompleteProfile = "//completeprofile";

    public const string Dashboard = "//dashboard";

    public const string Users = "users";

    public const string Groups = "groups";

    // Used for both "new group" and "edit group" (groupedit?groupId=...).
    public const string GroupEdit = "groupedit";

    // Admin: members of one group (groupmembers?groupId=...) and the add-members picker.
    public const string GroupMembers = "groupmembers";

    public const string AddMembers = "addmembers";

    // Regular user: my groups, and the list of groups I can join.
    public const string MyGroups = "mygroups";

    public const string JoinGroups = "joingroups";
}

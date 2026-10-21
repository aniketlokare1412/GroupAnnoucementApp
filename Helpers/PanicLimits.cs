namespace GroupAnnouncementApp.Helpers;

public static class PanicLimits
{
    // After pressing the panic button a member can press it again after this many minutes.
    public const int CooldownMinutes = 5;

    // Panic presses on the 5-minute notice (when the group has no open announcement) are stored under this
    // fixed id: groups/{groupId}/announcements/notice/panics. They are cleared when the admin posts a message.
    public const string NoticeAnnouncementId = "notice";
}

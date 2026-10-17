namespace GroupAnnouncementApp.ViewModels;

// One block of the member Home announcements: an optional super community heading, an optional
// community heading, and the cards of the joined groups inside. A member whose groups are in no
// community gets a single block with no headings, so Home looks exactly as before.
public sealed class MemberHomeSection
{
    // Shown only above the first community of a super community.
    public string SuperTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<MemberAnnouncementItem> Cards { get; set; } = new List<MemberAnnouncementItem>();

    public bool HasSuperTitle
    {
        get { return !string.IsNullOrWhiteSpace(SuperTitle); }
    }

    public bool HasTitle
    {
        get { return !string.IsNullOrWhiteSpace(Title); }
    }
}

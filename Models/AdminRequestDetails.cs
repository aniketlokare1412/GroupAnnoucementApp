namespace GroupAnnouncementApp.Models;

// What the member typed in the "Request admin access" form.
public sealed class AdminRequestDetails
{
    public string Name { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string CpfNumber { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string SubDivision { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string SubStationCode { get; set; } = string.Empty;

    // Optional ID card photo (JPEG or PNG, at most 200 KB).
    public AttachmentPick? IdCard { get; set; }
}

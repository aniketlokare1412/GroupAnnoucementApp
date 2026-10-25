namespace GroupAnnouncementApp.Helpers;

public static class AttachmentLimits
{
    // The file is stored inside Firestore (a copy per group), so a small limit keeps the free storage and download
    // quota safe. A file turns into text (about 4/3 larger). Photos are shrunk to fit; documents must already fit.
    // ALSO enforced in firestore.rules (size <= 204800, data <= 280000).
    public const int MaxKilobytes = 200;

    public const int MaxBytes = MaxKilobytes * 1024;

    // What the file picker offers: images, PDF, Word, Excel, text.
    public static readonly FilePickerFileType PickerTypes = new FilePickerFileType(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new string[]
                {
                    "image/*",
                    "application/pdf",
                    "application/msword",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/vnd.ms-excel",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "text/plain"
                }
            }
        });

    // Used when the picker does not report a content type.
    public static string GuessContentType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        switch (ext)
        {
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".png":
                return "image/png";
            case ".gif":
                return "image/gif";
            case ".webp":
                return "image/webp";
            case ".pdf":
                return "application/pdf";
            case ".doc":
                return "application/msword";
            case ".docx":
                return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            case ".xls":
                return "application/vnd.ms-excel";
            case ".xlsx":
                return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            case ".txt":
                return "text/plain";
            default:
                return "application/octet-stream";
        }
    }

    // Letters, digits, dot, dash and underscore only, so the name is always safe as a file name.
    public static string SafeFileName(string fileName)
    {
        string name = string.Empty;
        for (int i = 0; i < fileName.Length; i++)
        {
            char c = fileName[i];
            bool ok = char.IsAsciiLetterOrDigit(c) || c == '.' || c == '-' || c == '_';
            name = name + (ok ? c : '_');
        }

        return name.Length == 0 ? "file" : name;
    }
}

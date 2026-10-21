namespace GroupAnnouncementApp.Models;

// A file the admin picked, read into memory (at most 700 KB after shrinking).
public sealed class AttachmentPick
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    private byte[] _bytes = Array.Empty<byte>();
    private string? _base64;

    public byte[] Bytes
    {
        get { return _bytes; }
        set
        {
            _bytes = value;
            _base64 = null;
        }
    }

    // The file as base64 text, worked out once. Posting to many groups stores one copy per group,
    // so this saves repeating the conversion for every group.
    public string Base64
    {
        get
        {
            string? cached = _base64;
            if (cached == null)
            {
                cached = Convert.ToBase64String(_bytes);
                _base64 = cached;
            }

            return cached;
        }
    }
}

// What was stored for an announcement. Kind is "image" or "file".
public sealed class UploadedAttachment
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
}

// A stored file, read back.
public sealed class AttachmentFile
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
}

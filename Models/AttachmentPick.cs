namespace GroupAnnouncementApp.Models;

// A file the admin picked, read into memory (at most 700 KB after shrinking).
public sealed class AttachmentPick
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
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

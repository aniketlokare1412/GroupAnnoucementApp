using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Helpers;

public static class AttachmentOpener
{
    // Loads the stored document, saves it to the phone's cache and opens it with a viewer app.
    public static async Task OpenAsync(IAttachmentService attachments, string groupId, string announcementId, string? sharedFileId = null)
    {
        try
        {
            OperationResult<AttachmentFile> result = string.IsNullOrEmpty(sharedFileId)
                ? await attachments.GetAsync(groupId, announcementId)
                : await attachments.GetSharedAsync(sharedFileId);
            if (!result.IsSuccess || result.Value == null)
            {
                await TellAsync(result.ErrorMessage ?? "The file could not be loaded.");
                return;
            }

            string path = Path.Combine(FileSystem.CacheDirectory, AttachmentLimits.SafeFileName(result.Value.Name));
            await System.IO.File.WriteAllBytesAsync(path, result.Value.Bytes);
            await Launcher.Default.OpenAsync(new OpenFileRequest(result.Value.Name, new ReadOnlyFile(path, result.Value.ContentType)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN ATTACHMENT ERROR: " + ex);
            await TellAsync("We could not open the file. You may need an app that can open this type of file.");
        }
    }

    private static async Task TellAsync(string message)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.DisplayAlert("Attachment", message, "OK");
        }
    }
}

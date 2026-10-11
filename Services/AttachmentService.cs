using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IAuthService _authService;
    private readonly IAttachmentRepository _attachmentRepository;

    public AttachmentService(IAuthService authService, IAttachmentRepository attachmentRepository)
    {
        _authService = authService;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OperationResult<UploadedAttachment>> UploadAsync(string groupId, string announcementId, AttachmentPick file)
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null)
        {
            return OperationResult<UploadedAttachment>.Fail("Please sign in again.");
        }

        if (file.Bytes.Length == 0)
        {
            return OperationResult<UploadedAttachment>.Fail("The file is empty.");
        }

        if (file.Bytes.Length > AttachmentLimits.MaxBytes)
        {
            return OperationResult<UploadedAttachment>.Fail("The file is too large. The limit is " + AttachmentLimits.MaxKilobytes + " KB.");
        }

        try
        {
            AttachmentDocument document = new AttachmentDocument();
            document.Name = file.FileName;
            document.ContentType = file.ContentType;
            document.Size = file.Bytes.Length;
            document.Data = Convert.ToBase64String(file.Bytes);
            document.CreatedBy = uid;

            await ServiceHelper.WithTimeoutAsync(_attachmentRepository.CreateAsync(groupId, announcementId, document));

            UploadedAttachment uploaded = new UploadedAttachment();
            uploaded.Name = file.FileName;
            uploaded.Kind = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "file";
            return OperationResult<UploadedAttachment>.Success(uploaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SAVE ATTACHMENT ERROR: " + ex);
            return OperationResult<UploadedAttachment>.Fail(ServiceHelper.MapError(ex, "We could not save the file. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<AttachmentFile>> GetAsync(string groupId, string announcementId)
    {
        try
        {
            AttachmentDocument? document = await ServiceHelper.WithTimeoutAsync(_attachmentRepository.GetAsync(groupId, announcementId));
            if (document == null || string.IsNullOrEmpty(document.Data))
            {
                return OperationResult<AttachmentFile>.Fail("The file is no longer available.");
            }

            AttachmentFile file = new AttachmentFile();
            file.Name = document.Name;
            file.ContentType = document.ContentType;
            file.Bytes = Convert.FromBase64String(document.Data);
            return OperationResult<AttachmentFile>.Success(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ATTACHMENT ERROR: " + ex);
            return OperationResult<AttachmentFile>.Fail(ServiceHelper.MapError(ex, "We could not load the file. Please check your connection and try again."));
        }
    }

    public async Task DeleteAsync(string groupId, string announcementId)
    {
        try
        {
            await ServiceHelper.WithTimeoutAsync(_attachmentRepository.DeleteAsync(groupId, announcementId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE ATTACHMENT ERROR: " + ex);
        }
    }
}

using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Storage;

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

    public Task<OperationResult<UploadedAttachment>> UploadAsync(string groupId, string announcementId, AttachmentPick file)
    {
        return SaveAsync(document => _attachmentRepository.CreateAsync(groupId, announcementId, document), file);
    }

    public Task<OperationResult<UploadedAttachment>> UploadSharedAsync(string fileId, AttachmentPick file)
    {
        return SaveAsync(document => _attachmentRepository.CreateSharedAsync(fileId, document), file);
    }

    private async Task<OperationResult<UploadedAttachment>> SaveAsync(Func<AttachmentDocument, Task> save, AttachmentPick file)
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
            document.Data = file.Base64;
            document.CreatedBy = uid;

            await ServiceHelper.WithTimeoutAsync(save(document));

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

    // A stored file never changes (its document cannot be updated, a new announcement gets a new id), so a file
    // that was downloaded once is kept in memory. Home refreshes every 20 seconds and used to download every
    // picture again each time (1 read and up to 270 KB each). Small cache: the 30 most recent files.
    private const int MaxCachedFiles = 30;
    private static readonly Dictionary<string, AttachmentFile> FileCache = new Dictionary<string, AttachmentFile>();
    private static readonly Queue<string> FileCacheOrder = new Queue<string>();

    private static AttachmentFile? CachedFile(string key)
    {
        lock (FileCache)
        {
            AttachmentFile? found;
            return FileCache.TryGetValue(key, out found) ? found : null;
        }
    }

    private static void RememberFile(string key, AttachmentFile file)
    {
        lock (FileCache)
        {
            if (!FileCache.ContainsKey(key))
            {
                FileCacheOrder.Enqueue(key);
            }

            FileCache[key] = file;
            while (FileCacheOrder.Count > MaxCachedFiles)
            {
                FileCache.Remove(FileCacheOrder.Dequeue());
            }
        }
    }

    private static void ForgetFile(string key)
    {
        lock (FileCache)
        {
            FileCache.Remove(key);
        }
    }

    public Task<OperationResult<AttachmentFile>> GetAsync(string groupId, string announcementId)
    {
        return LoadAsync("g_" + groupId + "_" + announcementId, () => _attachmentRepository.GetAsync(groupId, announcementId));
    }

    public Task<OperationResult<AttachmentFile>> GetSharedAsync(string fileId)
    {
        return LoadAsync("s_" + fileId, () => _attachmentRepository.GetSharedAsync(fileId));
    }

    // Memory first, then the phone's cache folder (survives app restarts), then Firestore (1 read).
    // A stored file never changes, so a cached copy is always right.
    private async Task<OperationResult<AttachmentFile>> LoadAsync(string cacheKey, Func<Task<AttachmentDocument?>> fetch)
    {
        AttachmentFile? cached = CachedFile(cacheKey);
        if (cached == null)
        {
            cached = ReadFromDisk(cacheKey);
            if (cached != null)
            {
                RememberFile(cacheKey, cached);
            }
        }

        if (cached != null)
        {
            return OperationResult<AttachmentFile>.Success(cached);
        }

        try
        {
            AttachmentDocument? document = await ServiceHelper.WithTimeoutAsync(fetch());
            if (document == null || string.IsNullOrEmpty(document.Data))
            {
                return OperationResult<AttachmentFile>.Fail("The file is no longer available.");
            }

            AttachmentFile file = new AttachmentFile();
            file.Name = document.Name;
            file.ContentType = document.ContentType;
            file.Bytes = Convert.FromBase64String(document.Data);
            RememberFile(cacheKey, file);
            WriteToDisk(cacheKey, file);
            return OperationResult<AttachmentFile>.Success(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ATTACHMENT ERROR: " + ex);
            return OperationResult<AttachmentFile>.Fail(ServiceHelper.MapError(ex, "We could not load the file. Please check your connection and try again."));
        }
    }

    // ---- the phone's cache folder (best effort, never throws) ----
    private const int MaxDiskFiles = 40;

    private static string DiskFolder
    {
        get { return Path.Combine(FileSystem.CacheDirectory, "att"); }
    }

    private static string DiskPath(string key, string extension)
    {
        return Path.Combine(DiskFolder, key.Replace('/', '_') + extension);
    }

    private static AttachmentFile? ReadFromDisk(string key)
    {
        try
        {
            string binPath = DiskPath(key, ".bin");
            string metaPath = DiskPath(key, ".meta");
            if (!File.Exists(binPath) || !File.Exists(metaPath))
            {
                return null;
            }

            string[] meta = File.ReadAllLines(metaPath);
            if (meta.Length < 2)
            {
                return null;
            }

            AttachmentFile file = new AttachmentFile();
            file.Name = meta[0];
            file.ContentType = meta[1];
            file.Bytes = File.ReadAllBytes(binPath);
            return file.Bytes.Length == 0 ? null : file;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DISK CACHE READ ERROR: " + ex);
            return null;
        }
    }

    private static void WriteToDisk(string key, AttachmentFile file)
    {
        try
        {
            Directory.CreateDirectory(DiskFolder);
            File.WriteAllBytes(DiskPath(key, ".bin"), file.Bytes);
            File.WriteAllLines(DiskPath(key, ".meta"), new string[] { file.Name.Replace('\r', ' ').Replace('\n', ' '), file.ContentType });

            // Keep the folder small: the newest files stay.
            FileInfo[] files = new DirectoryInfo(DiskFolder).GetFiles("*.bin");
            if (files.Length > MaxDiskFiles)
            {
                Array.Sort(files, (x, y) => x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc));
                for (int i = 0; i < files.Length - MaxDiskFiles; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(files[i].Name);
                    files[i].Delete();
                    string meta = Path.Combine(DiskFolder, name + ".meta");
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DISK CACHE WRITE ERROR: " + ex);
        }
    }

    private static void DeleteFromDisk(string key)
    {
        try
        {
            string binPath = DiskPath(key, ".bin");
            string metaPath = DiskPath(key, ".meta");
            if (File.Exists(binPath))
            {
                File.Delete(binPath);
            }

            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DISK CACHE DELETE ERROR: " + ex);
        }
    }

    public async Task DeleteSharedAsync(string fileId)
    {
        try
        {
            ForgetFile("s_" + fileId);
            DeleteFromDisk("s_" + fileId);
            await ServiceHelper.WithTimeoutAsync(_attachmentRepository.DeleteSharedAsync(fileId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE SHARED ATTACHMENT ERROR: " + ex);
        }
    }

    public async Task DeleteAsync(string groupId, string announcementId)
    {
        try
        {
            ForgetFile("g_" + groupId + "_" + announcementId);
            DeleteFromDisk("g_" + groupId + "_" + announcementId);
            await ServiceHelper.WithTimeoutAsync(_attachmentRepository.DeleteAsync(groupId, announcementId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE ATTACHMENT ERROR: " + ex);
        }
    }
}

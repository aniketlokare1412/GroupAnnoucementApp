using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class DirectoryRepository : IDirectoryRepository
{
    private const string MainPath = "directory/main";
    private const string AdminPath = "directory/admin";

    private readonly IFirebaseFirestore _firestore;

    public DirectoryRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task<DirectoryDocument?> GetMainAsync()
    {
        return ReadAsync(MainPath, "Directory.GetMainAsync");
    }

    public Task<DirectoryDocument?> GetAdminAsync()
    {
        return ReadAsync(AdminPath, "Directory.GetAdminAsync");
    }

    public Task SetMainAsync(string json)
    {
        return WriteAsync(MainPath, json, "Directory.SetMainAsync");
    }

    public Task SetAdminAsync(string json)
    {
        return WriteAsync(AdminPath, json, "Directory.SetAdminAsync");
    }

    private async Task<DirectoryDocument?> ReadAsync(string path, string tag)
    {
        IDocumentReference document = _firestore.GetDocument(path);
        IDocumentSnapshot<DirectoryDocument> snapshot = await document.GetDocumentSnapshotAsync<DirectoryDocument>();
        FirestoreMeter.Read(tag, 1);
        return snapshot.Data;
    }

    private Task WriteAsync(string path, string json, string tag)
    {
        DirectoryDocument item = new DirectoryDocument();
        item.Data = json;
        item.Version = 1;

        IDocumentReference document = _firestore.GetDocument(path);
        FirestoreMeter.Write(tag);
        return document.SetDataAsync(item);
    }
}

using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class PanicRepository : IPanicRepository
{
    private readonly IFirebaseFirestore _firestore;

    public PanicRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string groupId, string announcementId, string uid)
    {
        PanicPress press = new PanicPress();
        press.UserId = uid;
        IDocumentReference document = _firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + uid);
        return document.SetDataAsync(press);
    }

    public async Task<bool> ExistsAsync(string groupId, string announcementId, string uid)
    {
        IDocumentReference document = _firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + uid);
        IDocumentSnapshot<PanicPress> snapshot = await document.GetDocumentSnapshotAsync<PanicPress>();
        return snapshot.Data != null;
    }

    public async Task<int> CountAsync(string groupId, string announcementId)
    {
        ICollectionReference collection = _firestore.GetCollection(CollectionPath(groupId, announcementId));
        IQuerySnapshot<PanicPress> snapshot = await collection.GetDocumentsAsync<PanicPress>();
        return snapshot.Documents.Count();
    }

    public async Task DeleteAllAsync(string groupId, string announcementId)
    {
        ICollectionReference collection = _firestore.GetCollection(CollectionPath(groupId, announcementId));
        IQuerySnapshot<PanicPress> snapshot = await collection.GetDocumentsAsync<PanicPress>();
        foreach (IDocumentSnapshot<PanicPress> document in snapshot.Documents)
        {
            PanicPress? press = document.Data;
            if (press != null)
            {
                await _firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + press.Id).DeleteDocumentAsync();
            }
        }
    }

    private static string CollectionPath(string groupId, string announcementId)
    {
        return "groups/" + groupId + "/announcements/" + announcementId + "/panics";
    }
}

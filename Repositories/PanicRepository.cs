using GroupAnnouncementApp.Helpers;
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
        // One document per press (uid + time), so a member can press again after the cooldown.
        string pressId = uid + "_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IDocumentReference document = _firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + pressId);
        FirestoreMeter.Write("Panic.CreateAsync");
        return document.SetDataAsync(press);
    }

    public async Task<bool> ExistsAsync(string groupId, string announcementId, string uid)
    {
        IDocumentReference document = _firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + uid);
        IDocumentSnapshot<PanicPress> snapshot = await document.GetDocumentSnapshotAsync<PanicPress>();
        FirestoreMeter.Read("Panic.ExistsAsync", 1);
        return snapshot.Data != null;
    }

    public async Task<DateTimeOffset?> LastPressAsync(string groupId, string announcementId, string uid)
    {
        IQuery query = _firestore.GetCollection(CollectionPath(groupId, announcementId)).WhereEqualsTo("userId", uid);
        IQuerySnapshot<PanicPress> snapshot = await query.GetDocumentsAsync<PanicPress>();
        FirestoreMeter.Read("Panic.LastPressAsync", snapshot.Documents.Count());

        DateTimeOffset? latest = null;
        foreach (IDocumentSnapshot<PanicPress> document in snapshot.Documents)
        {
            PanicPress? press = document.Data;
            if (press == null)
            {
                continue;
            }

            // A press just made can still have no server time: it is the newest.
            DateTimeOffset at = press.CreatedAt == default ? DateTimeOffset.UtcNow : press.CreatedAt;
            if (latest == null || at > latest.Value)
            {
                latest = at;
            }
        }

        return latest;
    }

    public async Task<int> CountAsync(string groupId, string announcementId)
    {
        ICollectionReference collection = _firestore.GetCollection(CollectionPath(groupId, announcementId));
        IQuerySnapshot<PanicPress> snapshot = await collection.GetDocumentsAsync<PanicPress>();
        FirestoreMeter.Read("Panic.CountAsync", snapshot.Documents.Count());
        return snapshot.Documents.Count();
    }

    public async Task DeleteAllAsync(string groupId, string announcementId)
    {
        ICollectionReference collection = _firestore.GetCollection(CollectionPath(groupId, announcementId));
        IQuerySnapshot<PanicPress> snapshot = await collection.GetDocumentsAsync<PanicPress>();
        FirestoreMeter.Read("Panic.DeleteAllAsync", snapshot.Documents.Count());
        // Repeated presses can add up to many documents: delete them side by side, 25 at a time.
        List<Task> deletions = new List<Task>();
        foreach (IDocumentSnapshot<PanicPress> document in snapshot.Documents)
        {
            PanicPress? press = document.Data;
            if (press != null)
            {
                FirestoreMeter.Delete("Panic.DeleteAllAsync");
                deletions.Add(_firestore.GetDocument(CollectionPath(groupId, announcementId) + "/" + press.Id).DeleteDocumentAsync());
                if (deletions.Count >= 25)
                {
                    await Task.WhenAll(deletions);
                    deletions.Clear();
                }
            }
        }

        if (deletions.Count > 0)
        {
            await Task.WhenAll(deletions);
        }
    }

    private static string CollectionPath(string groupId, string announcementId)
    {
        return "groups/" + groupId + "/announcements/" + announcementId + "/panics";
    }
}

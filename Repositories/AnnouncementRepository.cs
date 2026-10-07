using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class AnnouncementRepository : IAnnouncementRepository
{
    private const string GroupsCollection = "groups";
    private const string AnnouncementsCollection = "announcements";

    private readonly IFirebaseFirestore _firestore;

    public AnnouncementRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string groupId, string id, Announcement announcement)
    {
        IDocumentReference document = _firestore.GetDocument(DocumentPath(groupId, id));
        return document.SetDataAsync(announcement);
    }

    public async Task<Announcement?> GetByIdAsync(string groupId, string id)
    {
        IDocumentReference document = _firestore.GetDocument(DocumentPath(groupId, id));
        IDocumentSnapshot<Announcement> snapshot = await document.GetDocumentSnapshotAsync<Announcement>();
        return snapshot.Data;
    }

    public async Task<List<Announcement>> GetByGroupAsync(string groupId, bool activeOnly)
    {
        // Only query methods the rest of the app already uses (WhereEqualsTo, GetDocumentsAsync),
        // so no composite index is needed. Sorting and paging happen in AnnouncementService.
        IQuery query = _firestore.GetCollection(CollectionPath(groupId));
        if (activeOnly)
        {
            query = query.WhereEqualsTo("isActive", true);
        }

        IQuerySnapshot<Announcement> snapshot = await query.GetDocumentsAsync<Announcement>();

        List<Announcement> items = new List<Announcement>();
        foreach (IDocumentSnapshot<Announcement> document in snapshot.Documents)
        {
            Announcement? announcement = document.Data;
            if (announcement != null)
            {
                items.Add(announcement);
            }
        }

        return items;
    }

    public Task UpdateAsync(string groupId, string id, string title, string message)
    {
        IDocumentReference document = _firestore.GetDocument(DocumentPath(groupId, id));
        return document.UpdateDataAsync(
            ("title", (object?)title),
            ("message", (object?)message),
            ("isEdited", (object?)true));
    }

    public Task SetActiveAsync(string groupId, string id, bool isActive)
    {
        IDocumentReference document = _firestore.GetDocument(DocumentPath(groupId, id));
        return document.UpdateDataAsync(("isActive", (object?)isActive));
    }

    private static string CollectionPath(string groupId)
    {
        return GroupsCollection + "/" + groupId + "/" + AnnouncementsCollection;
    }

    private static string DocumentPath(string groupId, string id)
    {
        return CollectionPath(groupId) + "/" + id;
    }
}

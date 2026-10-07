using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class GroupRepository : IGroupRepository
{
    private const string GroupsCollection = "groups";

    private readonly IFirebaseFirestore _firestore;

    public GroupRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string id, AnnouncementGroup group)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        return document.SetDataAsync(group);
    }

    public async Task<AnnouncementGroup?> GetByIdAsync(string id)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        IDocumentSnapshot<AnnouncementGroup> snapshot = await document.GetDocumentSnapshotAsync<AnnouncementGroup>();
        return snapshot.Data;
    }

    public async Task<List<AnnouncementGroup>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(GroupsCollection);
        IQuerySnapshot<AnnouncementGroup> snapshot = await collection.GetDocumentsAsync<AnnouncementGroup>();

        List<AnnouncementGroup> groups = new List<AnnouncementGroup>();
        foreach (IDocumentSnapshot<AnnouncementGroup> document in snapshot.Documents)
        {
            AnnouncementGroup? group = document.Data;
            if (group != null)
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    public Task UpdateDetailsAsync(string id, string name, string description)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        return document.UpdateDataAsync(
            ("name", (object?)name),
            ("description", (object?)description));
    }

    public Task SetActiveAsync(string id, bool isActive)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        return document.UpdateDataAsync(("isActive", (object?)isActive));
    }
}

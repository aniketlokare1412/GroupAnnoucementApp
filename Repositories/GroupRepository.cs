using GroupAnnouncementApp.Helpers;
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
        FirestoreMeter.Write("Group.CreateAsync");
        return document.SetDataAsync(group);
    }

    public async Task<AnnouncementGroup?> GetByIdAsync(string id)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        IDocumentSnapshot<AnnouncementGroup> snapshot = await document.GetDocumentSnapshotAsync<AnnouncementGroup>();
        FirestoreMeter.Read("Group.GetByIdAsync", 1);
        return snapshot.Data;
    }

    public async Task<List<AnnouncementGroup>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(GroupsCollection);
        IQuerySnapshot<AnnouncementGroup> snapshot = await collection.GetDocumentsAsync<AnnouncementGroup>();
        FirestoreMeter.Read("Group.GetAllAsync", snapshot.Documents.Count());

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

    public async Task<List<AnnouncementGroup>> GetActiveAsync()
    {
        IQuery query = _firestore.GetCollection(GroupsCollection).WhereEqualsTo("isActive", true);
        IQuerySnapshot<AnnouncementGroup> snapshot = await query.GetDocumentsAsync<AnnouncementGroup>();
        FirestoreMeter.Read("Group.GetActiveAsync", snapshot.Documents.Count());

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

    public Task UpdateDetailsAsync(string id, string name, string description, string number)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        FirestoreMeter.Write("Group.UpdateDetailsAsync");
        return document.UpdateDataAsync(
            ("name", (object?)name),
            ("description", (object?)description),
            ("number", (object?)number));
    }

    public Task SetActiveAsync(string id, bool isActive)
    {
        IDocumentReference document = _firestore.GetDocument(GroupsCollection + "/" + id);
        FirestoreMeter.Write("Group.SetActiveAsync");
        return document.UpdateDataAsync(("isActive", (object?)isActive));
    }
}

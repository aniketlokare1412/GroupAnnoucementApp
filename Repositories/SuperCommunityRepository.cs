using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class SuperCommunityRepository : ISuperCommunityRepository
{
    private const string SuperCommunitiesCollection = "superCommunities";

    private readonly IFirebaseFirestore _firestore;

    public SuperCommunityRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string id, SuperCommunity superCommunity)
    {
        IDocumentReference document = _firestore.GetDocument(SuperCommunitiesCollection + "/" + id);
        FirestoreMeter.Write("SuperCommunity.CreateAsync");
        return document.SetDataAsync(superCommunity);
    }

    public async Task<SuperCommunity?> GetByIdAsync(string id)
    {
        IDocumentReference document = _firestore.GetDocument(SuperCommunitiesCollection + "/" + id);
        IDocumentSnapshot<SuperCommunity> snapshot = await document.GetDocumentSnapshotAsync<SuperCommunity>();
        FirestoreMeter.Read("SuperCommunity.GetByIdAsync", 1);
        return snapshot.Data;
    }

    public async Task<List<SuperCommunity>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(SuperCommunitiesCollection);
        IQuerySnapshot<SuperCommunity> snapshot = await collection.GetDocumentsAsync<SuperCommunity>();
        FirestoreMeter.Read("SuperCommunity.GetAllAsync", snapshot.Documents.Count());

        List<SuperCommunity> items = new List<SuperCommunity>();
        foreach (IDocumentSnapshot<SuperCommunity> document in snapshot.Documents)
        {
            SuperCommunity? item = document.Data;
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public Task UpdateDetailsAsync(string id, string name, string description, string number)
    {
        IDocumentReference document = _firestore.GetDocument(SuperCommunitiesCollection + "/" + id);
        FirestoreMeter.Write("SuperCommunity.UpdateDetailsAsync");
        return document.UpdateDataAsync(
            ("name", (object?)name),
            ("description", (object?)description),
            ("number", (object?)number));
    }
}

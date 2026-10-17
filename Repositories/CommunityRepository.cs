using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class CommunityRepository : ICommunityRepository
{
    private const string CommunitiesCollection = "communities";

    private readonly IFirebaseFirestore _firestore;

    public CommunityRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string id, Community community)
    {
        IDocumentReference document = _firestore.GetDocument(CommunitiesCollection + "/" + id);
        return document.SetDataAsync(community);
    }

    public async Task<Community?> GetByIdAsync(string id)
    {
        IDocumentReference document = _firestore.GetDocument(CommunitiesCollection + "/" + id);
        IDocumentSnapshot<Community> snapshot = await document.GetDocumentSnapshotAsync<Community>();
        return snapshot.Data;
    }

    public async Task<List<Community>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(CommunitiesCollection);
        IQuerySnapshot<Community> snapshot = await collection.GetDocumentsAsync<Community>();

        List<Community> communities = new List<Community>();
        foreach (IDocumentSnapshot<Community> document in snapshot.Documents)
        {
            Community? community = document.Data;
            if (community != null)
            {
                if (community.GroupIds == null)
                {
                    community.GroupIds = new List<string>();
                }

                communities.Add(community);
            }
        }

        return communities;
    }

    public Task UpdateAsync(string id, string name, string description, string number, List<string> groupIds, string superCommunityId)
    {
        IDocumentReference document = _firestore.GetDocument(CommunitiesCollection + "/" + id);
        return document.UpdateDataAsync(
            ("name", (object?)name),
            ("description", (object?)description),
            ("number", (object?)number),
            ("groupIds", (object?)groupIds),
            ("superCommunityId", (object?)superCommunityId));
    }

    public Task SetSuperCommunityAsync(string id, string superCommunityId)
    {
        IDocumentReference document = _firestore.GetDocument(CommunitiesCollection + "/" + id);
        return document.UpdateDataAsync(("superCommunityId", (object?)superCommunityId));
    }
}

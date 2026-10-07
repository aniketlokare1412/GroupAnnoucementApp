using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class UserRepository : IUserRepository
{
    private const string UsersCollection = "users";

    private readonly IFirebaseFirestore _firestore;

    public UserRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string uid, UserProfile profile)
    {
        IDocumentReference document = _firestore.GetDocument(UsersCollection + "/" + uid);
        return document.SetDataAsync(profile);
    }

    public async Task<UserProfile?> GetByIdAsync(string uid)
    {
        IDocumentReference document = _firestore.GetDocument(UsersCollection + "/" + uid);
        IDocumentSnapshot<UserProfile> snapshot = await document.GetDocumentSnapshotAsync<UserProfile>();
        return snapshot.Data;
    }

    public async Task<List<UserProfile>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(UsersCollection);
        IQuerySnapshot<UserProfile> snapshot = await collection.GetDocumentsAsync<UserProfile>();

        List<UserProfile> users = new List<UserProfile>();
        foreach (IDocumentSnapshot<UserProfile> document in snapshot.Documents)
        {
            UserProfile? profile = document.Data;
            if (profile != null)
            {
                users.Add(profile);
            }
        }

        return users;
    }

    public Task SetActiveAsync(string uid, bool isActive)
    {
        IDocumentReference document = _firestore.GetDocument(UsersCollection + "/" + uid);
        return document.UpdateDataAsync(("isActive", (object?)isActive));
    }

    public Task SetUserTypeAsync(string uid, string userType)
    {
        IDocumentReference document = _firestore.GetDocument(UsersCollection + "/" + uid);
        return document.UpdateDataAsync(("userType", (object?)userType));
    }
}

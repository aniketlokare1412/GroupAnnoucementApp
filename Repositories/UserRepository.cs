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
}

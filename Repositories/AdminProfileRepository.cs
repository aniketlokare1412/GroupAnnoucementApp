using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class AdminProfileRepository : IAdminProfileRepository
{
    private const string Collection = "adminProfiles";

    private readonly IFirebaseFirestore _firestore;

    public AdminProfileRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task SetAsync(string uid, AdminProfile profile)
    {
        IDocumentReference document = _firestore.GetDocument(Collection + "/" + uid);
        FirestoreMeter.Write("AdminProfile.SetAsync");
        return document.SetDataAsync(profile);
    }

    public async Task<AdminProfile?> GetAsync(string uid)
    {
        IDocumentReference document = _firestore.GetDocument(Collection + "/" + uid);
        IDocumentSnapshot<AdminProfile> snapshot = await document.GetDocumentSnapshotAsync<AdminProfile>();
        FirestoreMeter.Read("AdminProfile.GetAsync", 1);
        return snapshot.Data;
    }
}

using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class AdminRequestRepository : IAdminRequestRepository
{
    private const string RequestsCollection = "adminRequests";

    private readonly IFirebaseFirestore _firestore;

    public AdminRequestRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string uid, AdminRequest request)
    {
        IDocumentReference document = _firestore.GetDocument(RequestsCollection + "/" + uid);
        return document.SetDataAsync(request);
    }

    public async Task<AdminRequest?> GetByIdAsync(string uid)
    {
        IDocumentReference document = _firestore.GetDocument(RequestsCollection + "/" + uid);
        IDocumentSnapshot<AdminRequest> snapshot = await document.GetDocumentSnapshotAsync<AdminRequest>();
        return snapshot.Data;
    }

    public async Task<List<AdminRequest>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(RequestsCollection);
        IQuerySnapshot<AdminRequest> snapshot = await collection.GetDocumentsAsync<AdminRequest>();

        List<AdminRequest> requests = new List<AdminRequest>();
        foreach (IDocumentSnapshot<AdminRequest> document in snapshot.Documents)
        {
            AdminRequest? request = document.Data;
            if (request != null)
            {
                requests.Add(request);
            }
        }

        return requests;
    }

    public Task DeleteAsync(string uid)
    {
        IDocumentReference document = _firestore.GetDocument(RequestsCollection + "/" + uid);
        return document.DeleteDocumentAsync();
    }
}

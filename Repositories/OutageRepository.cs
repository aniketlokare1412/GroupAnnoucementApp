using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class OutageRepository : IOutageRepository
{
    private readonly IFirebaseFirestore _firestore;

    public OutageRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task SetAsync(string groupId, Outage outage)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("Outage.SetAsync");
        return document.SetDataAsync(outage);
    }

    public async Task<Outage?> GetAsync(string groupId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        IDocumentSnapshot<Outage> snapshot = await document.GetDocumentSnapshotAsync<Outage>();
        FirestoreMeter.Read("Outage.GetAsync", 1);
        return snapshot.Data;
    }

    public Task CancelAsync(string groupId, long cancelledAtMs)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("Outage.CancelAsync");
        return document.UpdateDataAsync(
            ("status", (object?)"Cancelled"),
            ("cancelledAtMs", (object?)cancelledAtMs));
    }

    public Task DeleteAsync(string groupId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Delete("Outage.DeleteAsync");
        return document.DeleteDocumentAsync();
    }

    private static string PathFor(string groupId)
    {
        return "groups/" + groupId + "/outages/current";
    }
}

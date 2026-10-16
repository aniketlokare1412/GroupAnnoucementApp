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
        return document.SetDataAsync(outage);
    }

    public async Task<Outage?> GetAsync(string groupId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        IDocumentSnapshot<Outage> snapshot = await document.GetDocumentSnapshotAsync<Outage>();
        return snapshot.Data;
    }

    public Task CancelAsync(string groupId, long cancelledAtMs)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        return document.UpdateDataAsync(
            ("status", (object?)"Cancelled"),
            ("cancelledAtMs", (object?)cancelledAtMs));
    }

    private static string PathFor(string groupId)
    {
        return "groups/" + groupId + "/outages/current";
    }
}

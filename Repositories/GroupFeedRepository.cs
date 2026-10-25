using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class GroupFeedRepository : IGroupFeedRepository
{
    private const string FeedCollection = "groupFeed";

    private readonly IFirebaseFirestore _firestore;

    public GroupFeedRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public async Task<GroupFeedDocument?> GetAsync(string groupId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        IDocumentSnapshot<GroupFeedDocument> snapshot = await document.GetDocumentSnapshotAsync<GroupFeedDocument>();
        FirestoreMeter.Read("GroupFeed.GetAsync", 1);
        return snapshot.Data;
    }

    public Task SetAsync(string groupId, GroupFeedDocument feed)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("GroupFeed.SetAsync");
        return document.SetDataAsync(feed);
    }

    public void QueueSet(FirestoreBatch batch, string groupId, GroupFeedDocument feed)
    {
        batch.Set(PathFor(groupId), feed, "GroupFeed.QueueSet");
    }

    public Task UpdateAsync(string groupId, params (string, object?)[] fields)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("GroupFeed.UpdateAsync");
        return document.UpdateDataAsync(fields);
    }

    // The one place that uses the Firestore snapshot listener. If the plugin's listener signature
    // differs from this, only this method needs changing.
    public IDisposable Watch(string groupId, Action<GroupFeedDocument?> onChanged, Action<Exception> onError)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        return document.AddSnapshotListener<GroupFeedDocument>(
            snapshot =>
            {
                // Counted once per delivery (an upper bound: a copy served from the phone's cache is free).
                FirestoreMeter.Read("GroupFeed.Watch", 1);
                onChanged(snapshot.Data);
            },
            error => onError(error));
    }

    private static string PathFor(string groupId)
    {
        return FeedCollection + "/" + groupId;
    }
}

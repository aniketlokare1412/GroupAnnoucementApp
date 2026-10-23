using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class GroupStatsRepository : IGroupStatsRepository
{
    private const string StatsCollection = "groupStats";
    private const int IncrementAttempts = 4;

    private readonly IFirebaseFirestore _firestore;

    public GroupStatsRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public async Task<GroupStatsDocument?> GetAsync(string groupId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        IDocumentSnapshot<GroupStatsDocument> snapshot = await document.GetDocumentSnapshotAsync<GroupStatsDocument>();
        FirestoreMeter.Read("GroupStats.GetAsync", 1);
        return snapshot.Data;
    }

    public async Task<List<GroupStatsDocument>> GetAllAsync()
    {
        ICollectionReference collection = _firestore.GetCollection(StatsCollection);
        IQuerySnapshot<GroupStatsDocument> snapshot = await collection.GetDocumentsAsync<GroupStatsDocument>();
        FirestoreMeter.Read("GroupStats.GetAllAsync", snapshot.Documents.Count());
        return ToList(snapshot);
    }

    public Task SetAsync(string groupId, GroupStatsDocument stats)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("GroupStats.SetAsync");
        return document.SetDataAsync(stats);
    }

    public void QueueSet(FirestoreBatch batch, string groupId, GroupStatsDocument stats)
    {
        batch.Set(PathFor(groupId), stats, "GroupStats.QueueSet");
    }

    public Task UpdateAsync(string groupId, params (string, object?)[] fields)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId));
        FirestoreMeter.Write("GroupStats.UpdateAsync");
        return document.UpdateDataAsync(fields);
    }

    // The counter. Only this method needs to change if the plugin offers a server-side increment
    // (FieldValue.Increment): the rules accept both, because the stored number must simply be the old one + 1.
    public async Task<bool> IncrementPanicAsync(string groupId, bool onNotice)
    {
        string field = onNotice ? "noticePanicCount" : "panicCount";
        Exception? last = null;

        for (int attempt = 0; attempt < IncrementAttempts; attempt++)
        {
            GroupStatsDocument? current = await GetAsync(groupId);
            if (current == null)
            {
                return false;
            }

            int next = (onNotice ? current.NoticePanicCount : current.PanicCount) + 1;
            try
            {
                await UpdateAsync(groupId, (field, (object?)next));
                return true;
            }
            catch (Exception ex)
            {
                // Another member pressed between our read and our write: the rules refuse the stale number.
                // Wait a moment (a different moment for every phone) and read again.
                last = ex;
                await Task.Delay(Random.Shared.Next(120, 500));
            }
        }

        throw last ?? new InvalidOperationException("The report count could not be updated.");
    }

    public IDisposable WatchAll(Action<List<GroupStatsDocument>> onChanged, Action<Exception> onError)
    {
        ICollectionReference collection = _firestore.GetCollection(StatsCollection);
        return collection.AddSnapshotListener<GroupStatsDocument>(
            snapshot =>
            {
                // Counted once per delivery (an upper bound: the first delivery reads every document, later ones
                // only the documents that changed).
                int changed = snapshot.Documents.Count();
                FirestoreMeter.Read("GroupStats.Watch", changed);
                onChanged(ToList(snapshot));
            },
            error => onError(error));
    }

    private static List<GroupStatsDocument> ToList(IQuerySnapshot<GroupStatsDocument> snapshot)
    {
        List<GroupStatsDocument> list = new List<GroupStatsDocument>();
        foreach (IDocumentSnapshot<GroupStatsDocument> document in snapshot.Documents)
        {
            GroupStatsDocument? data = document.Data;
            if (data != null)
            {
                list.Add(data);
            }
        }

        return list;
    }

    private static string PathFor(string groupId)
    {
        return StatsCollection + "/" + groupId;
    }
}

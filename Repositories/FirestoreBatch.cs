using GroupAnnouncementApp.Helpers;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

// A group of writes that is sent together (Phase 4). The repositories add their writes with Queue* methods,
// the service commits once.
//
// Two ways to commit, chosen at build time:
//  - default (safe, uses only calls the project already uses): the queued writes are sent side by side and
//    CommitAsync waits for all of them. Same behaviour as before Phase 4, nothing is atomic.
//  - USE_NATIVE_BATCH (add <DefineConstants>$(DefineConstants);USE_NATIVE_BATCH</DefineConstants> to the .csproj
//    after checking the plugin): ONE Firestore WriteBatch, all or nothing, one round trip. The lines inside
//    "#if USE_NATIVE_BATCH" use IFirebaseFirestore.CreateBatch() / IWriteBatch (Set, Update, Delete, CommitAsync);
//    if the plugin names differ, only this file needs changing.
// A batch may hold at most 500 writes, and security rules may look at about 20 other documents per batch:
// the broadcast code therefore sends at most 12 groups per batch.
public sealed class FirestoreBatch
{
    private readonly IFirebaseFirestore _firestore;
    private readonly object _gate = new object();
    private readonly List<Func<Task>> _singles = new List<Func<Task>>();

#if USE_NATIVE_BATCH
    private readonly List<Action<IWriteBatch>> _natives = new List<Action<IWriteBatch>>();
#endif

    public FirestoreBatch(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _singles.Count;
            }
        }
    }

    // Create or replace a whole document.
    public void Set<T>(string documentPath, T data, string tag) where T : class, IFirestoreObject
    {
        FirestoreMeter.Write(tag);
        lock (_gate)
        {
            _singles.Add(() => _firestore.GetDocument(documentPath).SetDataAsync(data));
#if USE_NATIVE_BATCH
            _natives.Add(batch => batch.Set(_firestore.GetDocument(documentPath), data));
#endif
        }
    }

    public void Delete(string documentPath, string tag)
    {
        FirestoreMeter.Delete(tag);
        lock (_gate)
        {
            _singles.Add(() => _firestore.GetDocument(documentPath).DeleteDocumentAsync());
#if USE_NATIVE_BATCH
            _natives.Add(batch => batch.Delete(_firestore.GetDocument(documentPath)));
#endif
        }
    }

    public async Task CommitAsync()
    {
        if (_singles.Count == 0)
        {
            return;
        }

#if USE_NATIVE_BATCH
        IWriteBatch native = _firestore.CreateBatch();
        for (int i = 0; i < _natives.Count; i++)
        {
            _natives[i](native);
        }

        await native.CommitAsync();
#else
        List<Task> tasks = new List<Task>();
        for (int i = 0; i < _singles.Count; i++)
        {
            tasks.Add(_singles[i]());
        }

        await Task.WhenAll(tasks);
#endif
    }
}

public interface IFirestoreBatchFactory
{
    FirestoreBatch Create();
}

public sealed class FirestoreBatchFactory : IFirestoreBatchFactory
{
    private readonly IFirebaseFirestore _firestore;

    public FirestoreBatchFactory(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public FirestoreBatch Create()
    {
        return new FirestoreBatch(_firestore);
    }
}

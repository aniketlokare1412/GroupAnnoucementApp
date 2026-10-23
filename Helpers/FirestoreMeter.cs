using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace GroupAnnouncementApp.Helpers;

// Phase 0 measurement. Every repository call counts the Firestore documents it reads, writes and deletes,
// by "Repository.Method". The totals are written to the debug log (Visual Studio Output / logcat, filter "[METER]"
// and "[PERF]") so a screen or action can be compared with the free-tier budget (50,000 reads, 20,000 writes,
// 20,000 deletes per day).
//
// Notes: a query that returns no documents still costs 1 read. Reads that security rules make (get / exists) are
// NOT counted here, they are extra: a panic press costs about 3 more. Counting is cheap and always on;
// logging only happens in debug builds.
public static class FirestoreMeter
{
    private sealed class Counts
    {
        public int Reads;
        public int Writes;
        public int Deletes;
    }

    private static readonly ConcurrentDictionary<string, Counts> ByTag = new ConcurrentDictionary<string, Counts>();

    private static int _reads;
    private static int _writes;
    private static int _deletes;

    public static int TotalReads
    {
        get { return Volatile.Read(ref _reads); }
    }

    public static int TotalWrites
    {
        get { return Volatile.Read(ref _writes); }
    }

    public static int TotalDeletes
    {
        get { return Volatile.Read(ref _deletes); }
    }

    public static void Read(string tag, int documents)
    {
        int count = documents < 1 ? 1 : documents;
        Interlocked.Add(ref _reads, count);
        Interlocked.Add(ref ByTag.GetOrAdd(tag, _ => new Counts()).Reads, count);
    }

    public static void Write(string tag)
    {
        Interlocked.Increment(ref _writes);
        Interlocked.Increment(ref ByTag.GetOrAdd(tag, _ => new Counts()).Writes);
    }

    public static void Delete(string tag)
    {
        Interlocked.Increment(ref _deletes);
        Interlocked.Increment(ref ByTag.GetOrAdd(tag, _ => new Counts()).Deletes);
    }

    public static void Reset()
    {
        ByTag.Clear();
        Interlocked.Exchange(ref _reads, 0);
        Interlocked.Exchange(ref _writes, 0);
        Interlocked.Exchange(ref _deletes, 0);
    }

    // A readable table, busiest first.
    public static string Summary()
    {
        List<KeyValuePair<string, Counts>> rows = new List<KeyValuePair<string, Counts>>(ByTag);
        rows.Sort((a, b) => (b.Value.Reads + b.Value.Writes + b.Value.Deletes).CompareTo(a.Value.Reads + a.Value.Writes + a.Value.Deletes));

        StringBuilder text = new StringBuilder();
        text.AppendLine("[METER] totals: reads " + TotalReads + ", writes " + TotalWrites + ", deletes " + TotalDeletes);
        for (int i = 0; i < rows.Count; i++)
        {
            text.AppendLine("[METER]   " + rows[i].Key + "  reads " + rows[i].Value.Reads + "  writes " + rows[i].Value.Writes + "  deletes " + rows[i].Value.Deletes);
        }

        return text.ToString();
    }

    public static void Dump()
    {
        Debug.WriteLine(Summary());
    }
}

// Times one action and logs how many documents were read / written / deleted while it ran:
//   using (PerfTimer.Start("Home.Load")) { ... }
// The document numbers are a difference of the global totals, so they also include anything else running at the same time.
public sealed class PerfTimer : IDisposable
{
    private readonly string _name;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly int _reads;
    private readonly int _writes;
    private readonly int _deletes;

    private PerfTimer(string name)
    {
        _name = name;
        _reads = FirestoreMeter.TotalReads;
        _writes = FirestoreMeter.TotalWrites;
        _deletes = FirestoreMeter.TotalDeletes;
    }

    public static PerfTimer Start(string name)
    {
        return new PerfTimer(name);
    }

    public void Dispose()
    {
        _watch.Stop();
        Debug.WriteLine("[PERF] " + _name + "  " + _watch.ElapsedMilliseconds + " ms  | reads +" + (FirestoreMeter.TotalReads - _reads)
            + "  writes +" + (FirestoreMeter.TotalWrites - _writes) + "  deletes +" + (FirestoreMeter.TotalDeletes - _deletes));
    }
}

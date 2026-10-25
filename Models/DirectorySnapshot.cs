using System.Text.Json.Serialization;

namespace GroupAnnouncementApp.Models;

// Phase 1: the "directory" is two small Firestore documents that hold the group / community /
// super community lists, so a screen needs 1 read instead of one read per group.
//
//   directory/main   active groups + communities + super communities  (any signed-in active user reads it)
//   directory/admin  inactive groups                                   (admins only)
//
// Members could always read active groups, communities and super communities, so main holds exactly
// what they could already see; inactive groups stay admin-only. The old collections remain the source
// of truth: the directory is rebuilt from them after every admin change (and by the admin Rebuild action).
//
// Each document stores its list as ONE JSON text field (compact, and independent of how the Firestore
// plugin maps nested objects). Not Firestore objects themselves: see DirectoryDocument.

public sealed class DirectoryGroupEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool IsActive { get; set; }
}

public sealed class DirectoryCommunityEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("groupIds")]
    public List<string> GroupIds { get; set; } = new List<string>();

    [JsonPropertyName("superId")]
    public string SuperCommunityId { get; set; } = string.Empty;
}

public sealed class DirectorySuperEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

// The JSON inside directory/main.
public sealed class DirectoryMainPayload
{
    [JsonPropertyName("groups")]
    public List<DirectoryGroupEntry> Groups { get; set; } = new List<DirectoryGroupEntry>();

    [JsonPropertyName("communities")]
    public List<DirectoryCommunityEntry> Communities { get; set; } = new List<DirectoryCommunityEntry>();

    [JsonPropertyName("supers")]
    public List<DirectorySuperEntry> SuperCommunities { get; set; } = new List<DirectorySuperEntry>();
}

// The JSON inside directory/admin.
public sealed class DirectoryAdminPayload
{
    [JsonPropertyName("inactiveGroups")]
    public List<DirectoryGroupEntry> InactiveGroups { get; set; } = new List<DirectoryGroupEntry>();
}

// Source-generated serializer metadata: no reflection, safe with trimming.
[JsonSerializable(typeof(DirectoryMainPayload))]
[JsonSerializable(typeof(DirectoryAdminPayload))]
internal partial class DirectoryJsonContext : JsonSerializerContext
{
}

// What the app works with: the same model classes the screens already use (without createdBy / createdAt,
// which no list screen shows). Not a Firestore document.
public sealed class DirectorySnapshot
{
    // Admin snapshot: every group. Member snapshot: active groups only.
    public List<AnnouncementGroup> Groups { get; set; } = new List<AnnouncementGroup>();

    public List<Community> Communities { get; set; } = new List<Community>();

    public List<SuperCommunity> SuperCommunities { get; set; } = new List<SuperCommunity>();

    // True when inactive groups are included (admin snapshot).
    public bool IncludesInactive { get; set; }

    public DateTime LoadedUtc { get; set; } = DateTime.UtcNow;

    public AnnouncementGroup? FindGroup(string id)
    {
        for (int i = 0; i < Groups.Count; i++)
        {
            if (Groups[i].Id == id)
            {
                return Groups[i];
            }
        }

        return null;
    }

    // Fresh copies, so a screen that sorts or edits its list never changes the cached snapshot.
    public List<AnnouncementGroup> CopyGroups(bool activeOnly)
    {
        List<AnnouncementGroup> copy = new List<AnnouncementGroup>();
        for (int i = 0; i < Groups.Count; i++)
        {
            if (!activeOnly || Groups[i].IsActive)
            {
                copy.Add(CloneGroup(Groups[i]));
            }
        }

        return copy;
    }

    public List<Community> CopyCommunities()
    {
        List<Community> copy = new List<Community>();
        for (int i = 0; i < Communities.Count; i++)
        {
            Community source = Communities[i];
            Community item = new Community();
            item.Id = source.Id;
            item.Name = source.Name;
            item.Number = source.Number;
            item.Description = source.Description;
            item.GroupIds = new List<string>(source.GroupIds);
            item.SuperCommunityId = source.SuperCommunityId;
            copy.Add(item);
        }

        return copy;
    }

    public List<SuperCommunity> CopySuperCommunities()
    {
        List<SuperCommunity> copy = new List<SuperCommunity>();
        for (int i = 0; i < SuperCommunities.Count; i++)
        {
            SuperCommunity source = SuperCommunities[i];
            SuperCommunity item = new SuperCommunity();
            item.Id = source.Id;
            item.Name = source.Name;
            item.Number = source.Number;
            item.Description = source.Description;
            copy.Add(item);
        }

        return copy;
    }

    public static AnnouncementGroup CloneGroup(AnnouncementGroup source)
    {
        AnnouncementGroup item = new AnnouncementGroup();
        item.Id = source.Id;
        item.Name = source.Name;
        item.Number = source.Number;
        item.Description = source.Description;
        item.IsActive = source.IsActive;
        return item;
    }
}

// Result of the admin "Rebuild directory" action.
public sealed class DirectoryRebuildInfo
{
    public int Groups { get; set; }

    public int Communities { get; set; }

    public int SuperCommunities { get; set; }
}

namespace GroupAnnouncementApp.Models;

// The whole community structure, loaded in two small reads, with lookups so screens never repeat
// the same loops. Not a Firestore document.
public sealed class CommunityDirectory
{
    public List<Community> Communities { get; set; } = new List<Community>();

    public List<SuperCommunity> SuperCommunities { get; set; } = new List<SuperCommunity>();

    public Community? FindCommunity(string id)
    {
        for (int i = 0; i < Communities.Count; i++)
        {
            if (Communities[i].Id == id)
            {
                return Communities[i];
            }
        }

        return null;
    }

    public SuperCommunity? FindSuperCommunity(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < SuperCommunities.Count; i++)
        {
            if (SuperCommunities[i].Id == id)
            {
                return SuperCommunities[i];
            }
        }

        return null;
    }

    // The super community a community sits under, or null.
    public SuperCommunity? SuperOf(Community community)
    {
        return FindSuperCommunity(community.SuperCommunityId);
    }

    public List<Community> CommunitiesOfSuper(string superCommunityId)
    {
        List<Community> result = new List<Community>();
        for (int i = 0; i < Communities.Count; i++)
        {
            if (Communities[i].SuperCommunityId == superCommunityId)
            {
                result.Add(Communities[i]);
            }
        }

        return result;
    }

    // Ids of every group in every community of a super community (each id once).
    public List<string> GroupIdsOfSuper(string superCommunityId)
    {
        List<string> ids = new List<string>();
        List<Community> inside = CommunitiesOfSuper(superCommunityId);
        for (int i = 0; i < inside.Count; i++)
        {
            for (int j = 0; j < inside[i].GroupIds.Count; j++)
            {
                if (!ids.Contains(inside[i].GroupIds[j]))
                {
                    ids.Add(inside[i].GroupIds[j]);
                }
            }
        }

        return ids;
    }

    // The communities a group is in (sorted by name).
    public List<Community> CommunitiesOfGroup(string groupId)
    {
        List<Community> result = new List<Community>();
        for (int i = 0; i < Communities.Count; i++)
        {
            if (Communities[i].GroupIds.Contains(groupId))
            {
                result.Add(Communities[i]);
            }
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}

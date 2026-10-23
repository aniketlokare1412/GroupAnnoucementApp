using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class MembershipRepository : IMembershipRepository
{
    private const string GroupsCollection = "groups";
    private const string MembersCollection = "members";

    private readonly IFirebaseFirestore _firestore;

    public MembershipRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task AddAsync(string groupId, string userId, string addedBy)
    {
        GroupMember member = new GroupMember();
        member.UserId = userId;
        member.GroupId = groupId;
        member.AddedBy = addedBy;

        IDocumentReference document = _firestore.GetDocument(MemberPath(groupId, userId));
        FirestoreMeter.Write("Membership.AddAsync");
        return document.SetDataAsync(member);
    }

    public Task RemoveAsync(string groupId, string userId)
    {
        IDocumentReference document = _firestore.GetDocument(MemberPath(groupId, userId));
        FirestoreMeter.Delete("Membership.RemoveAsync");
        return document.DeleteDocumentAsync();
    }

    public async Task<GroupMember?> GetAsync(string groupId, string userId)
    {
        IDocumentReference document = _firestore.GetDocument(MemberPath(groupId, userId));
        IDocumentSnapshot<GroupMember> snapshot = await document.GetDocumentSnapshotAsync<GroupMember>();
        FirestoreMeter.Read("Membership.GetAsync", 1);
        return snapshot.Data;
    }

    public async Task<List<GroupMember>> GetByGroupAsync(string groupId)
    {
        ICollectionReference collection = _firestore.GetCollection(GroupsCollection + "/" + groupId + "/" + MembersCollection);
        IQuerySnapshot<GroupMember> snapshot = await collection.GetDocumentsAsync<GroupMember>();
        FirestoreMeter.Read("Membership.GetByGroupAsync", snapshot.Documents.Count());

        List<GroupMember> members = new List<GroupMember>();
        foreach (IDocumentSnapshot<GroupMember> document in snapshot.Documents)
        {
            GroupMember? member = document.Data;
            if (member != null)
            {
                members.Add(member);
            }
        }

        return members;
    }

    public async Task<List<string>> GetGroupIdsForUserAsync(string userId)
    {
        // Needs the collection group index on members.userId (see firestore.indexes.json).
        IQuery query = _firestore.GetCollectionGroup(MembersCollection).WhereEqualsTo("userId", userId);
        IQuerySnapshot<GroupMember> snapshot = await query.GetDocumentsAsync<GroupMember>();
        FirestoreMeter.Read("Membership.GetGroupIdsForUserAsync", snapshot.Documents.Count());

        List<string> groupIds = new List<string>();
        foreach (IDocumentSnapshot<GroupMember> document in snapshot.Documents)
        {
            GroupMember? member = document.Data;
            if (member != null && !string.IsNullOrEmpty(member.GroupId))
            {
                groupIds.Add(member.GroupId);
            }
        }

        return groupIds;
    }

    private static string MemberPath(string groupId, string userId)
    {
        return GroupsCollection + "/" + groupId + "/" + MembersCollection + "/" + userId;
    }
}

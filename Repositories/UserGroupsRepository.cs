using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class UserGroupsRepository : IUserGroupsRepository
{
    private const string Collection = "userGroups";

    private readonly IFirebaseFirestore _firestore;

    public UserGroupsRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public async Task<List<string>?> GetAsync(string userId)
    {
        IDocumentReference document = _firestore.GetDocument(Collection + "/" + userId);
        IDocumentSnapshot<UserGroupsDocument> snapshot = await document.GetDocumentSnapshotAsync<UserGroupsDocument>();
        FirestoreMeter.Read("UserGroups.GetAsync", 1);
        return ToList(snapshot.Data);
    }

    public Task SetAsync(string userId, List<string> groupIds)
    {
        UserGroupsDocument item = new UserGroupsDocument();
        item.GroupIds = groupIds;

        IDocumentReference document = _firestore.GetDocument(Collection + "/" + userId);
        FirestoreMeter.Write("UserGroups.SetAsync");
        return document.SetDataAsync(item);
    }

    public IDisposable Watch(string userId, Action<List<string>?> onChanged, Action<Exception> onError)
    {
        IDocumentReference document = _firestore.GetDocument(Collection + "/" + userId);
        return document.AddSnapshotListener<UserGroupsDocument>(
            snapshot =>
            {
                FirestoreMeter.Read("UserGroups.Watch", 1);
                onChanged(ToList(snapshot.Data));
            },
            error => onError(error));
    }

    private static List<string>? ToList(UserGroupsDocument? document)
    {
        if (document == null)
        {
            return null;
        }

        List<string> ids = new List<string>();
        if (document.GroupIds != null)
        {
            for (int i = 0; i < document.GroupIds.Count; i++)
            {
                string id = document.GroupIds[i];
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}

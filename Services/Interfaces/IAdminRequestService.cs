using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// A member asks to become an admin; an admin approves or declines. The Firestore rules are the
// real protection: only the person can create their own request, only admins can read all of them.
public interface IAdminRequestService
{
    // Member: true when this person already has an open request.
    Task<OperationResult<bool>> HasPendingRequestAsync();

    // Member: sends the request (does nothing if one is already open).
    Task<OperationResult> RequestAsync();

    // Admin: open requests, oldest first.
    Task<OperationResult<List<AdminRequest>>> GetPendingAsync();

    // Admin: makes the person an admin and closes the request.
    Task<OperationResult> ApproveAsync(string uid);

    // Admin: closes the request without changing anything.
    Task<OperationResult> DeclineAsync(string uid);
}

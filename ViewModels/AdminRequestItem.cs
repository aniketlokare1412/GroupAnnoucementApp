using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One row on the Admin requests page. Carries its own commands and accessibility labels.
public sealed class AdminRequestItem
{
    public string Id { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;

    public ICommand? ApproveCommand { get; private set; }
    public ICommand? DeclineCommand { get; private set; }

    public string ApproveLabel
    {
        get { return "Approve " + Phone + " as administrator"; }
    }

    public string DeclineLabel
    {
        get { return "Decline request from " + Phone; }
    }

    public static AdminRequestItem From(
        AdminRequest request,
        Func<AdminRequestItem, Task> onApprove,
        Func<AdminRequestItem, Task> onDecline)
    {
        AdminRequestItem item = new AdminRequestItem();
        item.Id = request.Id;
        item.Phone = string.IsNullOrWhiteSpace(request.Phone) ? "Unknown number" : request.Phone;

        string digits = PhoneLogin.Normalize(item.Phone);
        item.Initials = digits.Length >= 2 ? digits.Substring(digits.Length - 2) : "#";

        item.ApproveCommand = new AsyncRelayCommand(() => onApprove(item));
        item.DeclineCommand = new AsyncRelayCommand(() => onDecline(item));
        return item;
    }
}

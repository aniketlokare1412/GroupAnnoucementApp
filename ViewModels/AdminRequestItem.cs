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

    // The details the person filled in (empty for requests made before the form existed).
    public string DisplayName { get; private set; } = string.Empty;
    public string DesignationText { get; private set; } = string.Empty;
    public string PlaceText { get; private set; } = string.Empty;
    public string SectionText { get; private set; } = string.Empty;
    public bool HasDetails { get; private set; }

    public bool HasNoDetails
    {
        get { return !HasDetails; }
    }

    public string IdCardName { get; private set; } = string.Empty;
    public string IdCardContentType { get; private set; } = string.Empty;
    public string IdCardData { get; private set; } = string.Empty;

    public bool HasIdCard
    {
        get { return IdCardData.Length > 0; }
    }

    public ICommand? ViewIdCardCommand { get; private set; }

    public ICommand? ApproveCommand { get; private set; }
    public ICommand? DeclineCommand { get; private set; }

    public string ApproveLabel
    {
        get { return "Approve " + DisplayName + " as administrator"; }
    }

    public string DeclineLabel
    {
        get { return "Decline request from " + DisplayName; }
    }

    public static AdminRequestItem From(
        AdminRequest request,
        Func<AdminRequestItem, Task> onApprove,
        Func<AdminRequestItem, Task> onDecline,
        Func<AdminRequestItem, Task> onViewIdCard)
    {
        AdminRequestItem item = new AdminRequestItem();
        item.Id = request.Id;
        item.Phone = string.IsNullOrWhiteSpace(request.Phone) ? "Unknown number" : request.Phone;

        string digits = PhoneLogin.Normalize(item.Phone);
        item.Initials = !string.IsNullOrWhiteSpace(request.Name)
            ? InitialsHelper.From(request.Name)
            : (digits.Length >= 2 ? digits.Substring(digits.Length - 2) : "#");

        item.DisplayName = string.IsNullOrWhiteSpace(request.Name) ? item.Phone : request.Name;
        item.HasDetails = !string.IsNullOrWhiteSpace(request.Designation) || !string.IsNullOrWhiteSpace(request.CpfNumber);
        item.DesignationText = request.Designation + " - CPF " + request.CpfNumber;
        item.PlaceText = "Division: " + request.Division + " | Sub Division: " + request.SubDivision;
        item.SectionText = "Section: " + request.Section + " | Sub Station Code: " + request.SubStationCode;
        item.IdCardName = request.IdCardName;
        item.IdCardContentType = request.IdCardContentType;
        item.IdCardData = request.IdCardData;

        item.ViewIdCardCommand = new AsyncRelayCommand(() => onViewIdCard(item));
        item.ApproveCommand = new AsyncRelayCommand(() => onApprove(item));
        item.DeclineCommand = new AsyncRelayCommand(() => onDecline(item));
        return item;
    }
}

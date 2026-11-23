using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// "Request admin access": the member fills in these details first; only then the request is sent.
// All fields are required except the ID card photo.
public partial class AdminRequestFormViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAdminRequestService _requests;
    private readonly IToastService _toasts;

    private AttachmentPick? _idCard;
    private bool _isLoaded;

    [ObservableProperty]
    private string _name = string.Empty;

    // The designation drop-down. -1 = nothing chosen yet.
    [ObservableProperty]
    private int _designationIndex = -1;

    [ObservableProperty]
    private string _cpfNumber = string.Empty;

    [ObservableProperty]
    private string _division = string.Empty;

    [ObservableProperty]
    private string _subDivision = string.Empty;

    [ObservableProperty]
    private string _section = string.Empty;

    [ObservableProperty]
    private string _subStationCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIdCard))]
    [NotifyPropertyChangedFor(nameof(IdCardSummary))]
    private string _idCardName = string.Empty;

    public List<string> Designations { get; } = new List<string>(AdminRequestLimits.Designations);

    public int MaxTextLength
    {
        get { return AdminRequestLimits.MaxTextLength; }
    }

    public bool HasIdCard
    {
        get { return IdCardName.Length > 0; }
    }

    public string IdCardSummary
    {
        get { return HasIdCard ? IdCardName : "Add your ID card photo (optional, JPEG or PNG)"; }
    }

    public AdminRequestFormViewModel(
        INavigationService navigation,
        ISessionService session,
        IAdminRequestService requests,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _requests = requests;
        _toasts = toasts;
    }

    // Called by the page every time it appears. The name starts as the profile name and can be changed.
    public async Task LoadAsync()
    {
        if (_session.IsAdmin)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        if (_isLoaded)
        {
            return;
        }

        // Starts as the profile name (left empty when the profile name is really a phone number).
        string profileName = _session.CurrentProfile?.Name ?? string.Empty;
        Name = NameHelper.LooksLikePhone(profileName) ? string.Empty : profileName;
        _isLoaded = true;
    }

    [RelayCommand]
    private async Task PickIdCardAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            PickOptions options = new PickOptions();
            options.PickerTitle = "Choose your ID card photo";
            options.FileTypes = AdminRequestLimits.IdCardPickerTypes;

            FileResult? file = await FilePicker.Default.PickAsync(options);
            if (file == null)
            {
                return;
            }

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                ErrorMessage = "Please choose a JPEG or PNG photo.";
                return;
            }

            using Stream stream = await file.OpenReadAsync();
            using MemoryStream buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            byte[] bytes = buffer.ToArray();
            string contentType = extension == ".png" ? "image/png" : "image/jpeg";
            string fileName = file.FileName;

            // A big photo is made smaller (it becomes a JPEG) so it fits in the request.
            if (bytes.Length > AttachmentLimits.MaxBytes)
            {
                byte[]? smaller = await Task.Run(() => ImageShrinker.Shrink(bytes, AttachmentLimits.MaxBytes));
                if (smaller == null)
                {
                    ErrorMessage = "This photo is too large. Please choose a smaller one.";
                    return;
                }

                bytes = smaller;
                contentType = "image/jpeg";
                fileName = Path.GetFileNameWithoutExtension(fileName) + ".jpg";
            }

            AttachmentPick pick = new AttachmentPick();
            pick.FileName = fileName;
            pick.ContentType = contentType;
            pick.Bytes = bytes;

            _idCard = pick;
            IdCardName = pick.FileName;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PICK ID CARD ERROR: " + ex);
            ErrorMessage = "We could not read that photo. Please try another one.";
        }
    }

    [RelayCommand]
    private void ClearIdCard()
    {
        _idCard = null;
        IdCardName = string.Empty;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        AdminRequestDetails details = new AdminRequestDetails();
        details.Name = Name;
        details.Designation = DesignationIndex >= 0 && DesignationIndex < Designations.Count ? Designations[DesignationIndex] : string.Empty;
        details.CpfNumber = CpfNumber;
        details.Division = Division;
        details.SubDivision = SubDivision;
        details.Section = Section;
        details.SubStationCode = SubStationCode;
        details.IdCard = _idCard;

        IsBusy = true;
        try
        {
            OperationResult result = await _requests.RequestAsync(details);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _toasts.Show("Admin request sent");
            await _navigation.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

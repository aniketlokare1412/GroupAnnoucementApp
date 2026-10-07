namespace GroupAnnouncementApp.Services.Interfaces;

// Lets view models show dialogs without touching pages or UI classes.
public interface IDialogService
{
    Task AlertAsync(string title, string message, string closeText);

    // Returns true when the user taps the accept button.
    Task<bool> ConfirmAsync(string title, string message, string acceptText, string cancelText);

    // Shows a list of options. Returns the chosen option, or null if cancelled.
    Task<string?> ChooseAsync(string title, string cancelText, string[] options);
}

using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class DialogService : IDialogService
{
    public async Task AlertAsync(string title, string message, string closeText)
    {
        Page? page = GetCurrentPage();
        if (page == null)
        {
            return;
        }

        await page.DisplayAlertAsync(title, message, closeText);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string acceptText, string cancelText)
    {
        Page? page = GetCurrentPage();
        if (page == null)
        {
            return false;
        }

        return await page.DisplayAlertAsync(title, message, acceptText, cancelText);
    }

    public async Task<string?> ChooseAsync(string title, string cancelText, string[] options)
    {
        Page? page = GetCurrentPage();
        if (page == null)
        {
            return null;
        }

        string? choice = await page.DisplayActionSheetAsync(title, cancelText, null!, options);
        if (choice == null || choice == cancelText)
        {
            return null;
        }

        return choice;
    }

    private static Page? GetCurrentPage()
    {
        if (Shell.Current == null)
        {
            return null;
        }

        return Shell.Current.CurrentPage;
    }
}

namespace GroupAnnouncementApp.Helpers;

// Small helpers shared by the membership services (timeout and error mapping).
public static class ServiceHelper
{
    public const int TimeoutSeconds = 20;

    // Throws TimeoutException when the task takes longer than TimeoutSeconds.
    public static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
        if (finished != task)
        {
            throw new TimeoutException();
        }

        return await task;
    }

    public static async Task WithTimeoutAsync(Task task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
        if (finished != task)
        {
            throw new TimeoutException();
        }

        await task;
    }

    public static string MapError(Exception ex, string fallbackMessage)
    {
        string text = (ex.GetType().FullName + " " + ex.Message).ToLowerInvariant();

        if (text.Contains("permission"))
        {
            return "You do not have permission to do that.";
        }

        // Firestore needs an index that has not been deployed yet, or is still being built.
        if (text.Contains("failed_precondition") || text.Contains("the query requires"))
        {
            return AddDebugDetails("This feature needs a database index that is not ready yet. Please try again in a few minutes.", ex);
        }

        return AddDebugDetails(fallbackMessage, ex);
    }

    // In debug builds the real error text is appended in [brackets], so a tester can see
    // WHY something failed. Release builds only show the friendly message.
    private static string AddDebugDetails(string message, Exception ex)
    {
#if DEBUG
        return message + " [" + ex.Message + "]";
#else
        return message;
#endif
    }
}

using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Services.Interfaces;
using Plugin.Firebase.Auth;

namespace GroupAnnouncementApp.Services;

public class AuthService : IAuthService
{
    private readonly IFirebaseAuth _auth;

    public AuthService(IFirebaseAuth auth)
    {
        _auth = auth;
    }

    public bool IsAuthenticated
    {
        get { return _auth.CurrentUser != null; }
    }

    public string? GetCurrentUserId()
    {
        IFirebaseUser? user = _auth.CurrentUser;
        if (user == null)
        {
            return null;
        }
        return user.Uid;
    }

    public string? GetCurrentUserEmail()
    {
        IFirebaseUser? user = _auth.CurrentUser;
        if (user == null)
        {
            return null;
        }
        return user.Email;
    }

    public async Task<OperationResult> LoginAsync(string email, string password)
    {
        try
        {
            // IMPORTANT: createsUserAutomatically must be false.
            // The plugin default is true, which would silently create a new
            // Firebase account whenever someone types an unknown email.
            await _auth.SignInWithEmailAndPasswordAsync(email, password, false);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(WithDebugDetails(MapLoginError(ex), ex));
        }
    }

    public async Task<OperationResult> LogoutAsync()
    {
        try
        {
            await _auth.SignOutAsync();
            return OperationResult.Success();
        }
        catch (Exception)
        {
            return OperationResult.Fail("Could not log out. Please try again.");
        }
    }

    public async Task<OperationResult<string>> RegisterAsync(string email, string password)
    {
        try
        {
            IFirebaseUser user = await _auth.CreateUserAsync(email, password);
            return OperationResult<string>.Success(user.Uid);
        }
        catch (Exception ex)
        {
            return OperationResult<string>.Fail(WithDebugDetails(MapRegisterError(ex), ex));
        }
    }

    public async Task<OperationResult> DeleteCurrentUserAsync()
    {
        IFirebaseUser? user = _auth.CurrentUser;
        try
        {
            if (user != null)
            {
                await user.DeleteAsync();
            }
            return OperationResult.Success();
        }
        catch (Exception)
        {
            // Deleting failed (for example no network). At least sign out.
            try
            {
                await _auth.SignOutAsync();
            }
            catch (Exception)
            {
                // nothing more we can do here
            }
            return OperationResult.Fail("Could not remove the incomplete account.");
        }
    }

    private static string WithDebugDetails(string friendlyMessage, Exception ex)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine("AUTH ERROR: " + BuildErrorText(ex));
        return friendlyMessage + " [" + ex.GetType().Name + ": " + ex.Message + "]";
#else
    return friendlyMessage;
#endif
    }

    // Looks at the exception type name and message text (including inner
    // exceptions) and returns a friendly message. Order of the checks matters.
    private static string MapLoginError(Exception ex)
    {
        string text = BuildErrorText(ex);

        if (IsNetworkProblem(text))
        {
            return "No internet connection. Please check your network and try again.";
        }

        if (text.Contains("disabled"))
        {
            return "This account has been disabled. Please contact your admin.";
        }

        if (IsTooManyRequests(text))
        {
            return "Too many attempts. Please wait a few minutes and try again.";
        }

        if (text.Contains("badly formatted"))
        {
            return "Please enter a valid email address.";
        }

        if (text.Contains("invalidcredentials") || text.Contains("invaliduser")
            || text.Contains("auth credential") || text.Contains("password is invalid")
            || text.Contains("no user record") || text.Contains("invalid_login_credentials")
            || text.Contains("usernotfound") || text.Contains("invalidcredential"))
        {
            return "Incorrect email or password.";
        }

        return "Login failed. Please try again.";
    }

    private static string MapRegisterError(Exception ex)
    {
        string text = BuildErrorText(ex);

        if (IsNetworkProblem(text))
        {
            return "No internet connection. Please check your network and try again.";
        }

        if (text.Contains("usercollision") || text.Contains("already in use")
            || text.Contains("email_exists") || text.Contains("emailalreadyinuse"))
        {
            return "An account with this email already exists. Try logging in instead.";
        }

        if (text.Contains("weakpassword") || text.Contains("weak password")
            || text.Contains("at least 6"))
        {
            return "Password is too weak. Please use at least 6 characters.";
        }

        if (text.Contains("badly formatted") || text.Contains("invalidemail"))
        {
            return "Please enter a valid email address.";
        }

        if (IsTooManyRequests(text))
        {
            return "Too many attempts. Please wait a few minutes and try again.";
        }

        return "Registration failed. Please try again.";
    }

    private static bool IsNetworkProblem(string text)
    {
        return text.Contains("network") || text.Contains("unable to resolve host")
            || text.Contains("timeout") || text.Contains("timed out")
            || text.Contains("unreachable");
    }

    private static bool IsTooManyRequests(string text)
    {
        return text.Contains("too many") || text.Contains("toomanyrequests")
            || text.Contains("unusual activity");
    }

    private static string BuildErrorText(Exception ex)
    {
        string text = string.Empty;
        Exception? current = ex;
        int depth = 0;

        while (current != null && depth < 5)
        {
            text = text + " " + current.GetType().FullName + " " + current.Message;
            current = current.InnerException;
            depth++;
        }

        return text.ToLowerInvariant();
    }
}

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace HMailServer.ComInterop;

public enum WebAdminBrokerTokenType
{
    Unknown = 0,
    Primary = 1,
    Impersonation = 2
}

public enum WebAdminBrokerImpersonationLevel
{
    Anonymous = 0,
    Identification = 1,
    Impersonation = 2,
    Delegation = 3
}

[ComVisible(false)]
public sealed record WebAdminBrokerCallerIdentity(
    string? Sid,
    WebAdminBrokerTokenType TokenType,
    WebAdminBrokerImpersonationLevel ImpersonationLevel,
    bool IsRemote);

// The Windows COM capture implementation is intentionally separate from this
// policy boundary so tests cannot accidentally authorize a caller by naming an
// expected SID in the broker method arguments.
[ComVisible(false)]
public interface IWebAdminBrokerCallerIdentitySource
{
    WebAdminBrokerCallerIdentity? CaptureImpersonatedCaller();

    bool RevertToSelf();
}

[ComVisible(false)]
public sealed class WebAdminSessionBrokerCallerGuard
{
    private const int EAccessDenied = unchecked((int)0x80070005);

    private readonly string _configuredWorkerSid;
    private readonly IWebAdminBrokerCallerIdentitySource _identitySource;

    public WebAdminSessionBrokerCallerGuard(
        string configuredWorkerSid,
        IWebAdminBrokerCallerIdentitySource identitySource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredWorkerSid);
        ArgumentNullException.ThrowIfNull(identitySource);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The WebAdmin broker caller guard requires Windows SID support.");
        }

        try
        {
            _configuredWorkerSid = NormalizeSid(configuredWorkerSid);
        }
        catch (Exception exception) when (exception is ArgumentException or PlatformNotSupportedException)
        {
            throw new ArgumentException("The configured WebAdmin worker SID is invalid.", nameof(configuredWorkerSid), exception);
        }

        _identitySource = identitySource;
    }

    public T Invoke<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        COMException? authorizationFailure = null;

        try
        {
            var caller = _identitySource.CaptureImpersonatedCaller();
            if (!IsAuthorized(caller))
            {
                authorizationFailure = AccessDenied();
            }
        }
        catch
        {
            authorizationFailure = AccessDenied();
        }
        finally
        {
            try
            {
                if (!_identitySource.RevertToSelf())
                {
                    authorizationFailure = AccessDenied();
                }
            }
            catch
            {
                authorizationFailure = AccessDenied();
            }
        }

        if (authorizationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(authorizationFailure).Throw();
        }

        // The operation runs only after the caller token has been reverted.
        // Its own failure is therefore preserved without weakening the gate.
        return operation();
    }

    private bool IsAuthorized(WebAdminBrokerCallerIdentity? caller) =>
        caller is not null
        && !caller.IsRemote
        && caller.TokenType == WebAdminBrokerTokenType.Impersonation
        && caller.ImpersonationLevel >= WebAdminBrokerImpersonationLevel.Identification
        && caller.Sid is not null
        && SidMatches(caller.Sid);

    private bool SidMatches(string sid)
    {
        try
        {
            return StringComparer.OrdinalIgnoreCase.Equals(
                _configuredWorkerSid,
                NormalizeSid(sid));
        }
        catch (Exception exception) when (exception is ArgumentException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static COMException AccessDenied() =>
        new("WebAdmin broker caller authorization failed.", EAccessDenied);

    private static string NormalizeSid(string sid)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The WebAdmin broker caller guard requires Windows SID support.");
        }

        return new SecurityIdentifier(sid).Value;
    }
}

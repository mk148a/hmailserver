namespace HMailServer.Storage.SqlServer;

public sealed record SmtpGreylistingOptions
{
    private int _enabled;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public bool SkipAuthenticated { get; init; } = true;

    public bool BypassOnSpfPass { get; init; }

    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan InitialRecordLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan PassedRecordLifetime { get; init; } = TimeSpan.FromDays(36);

    public string FailureResponse { get; init; } = "451 Please try again later.";
}

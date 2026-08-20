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

    private long _initialDelayTicks = TimeSpan.FromMinutes(30).Ticks;

    public TimeSpan InitialDelay
    {
        get => TimeSpan.FromTicks(Volatile.Read(ref _initialDelayTicks));
        set => Volatile.Write(ref _initialDelayTicks, value.Ticks);
    }

    public TimeSpan InitialRecordLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan PassedRecordLifetime { get; init; } = TimeSpan.FromDays(36);

    public string FailureResponse { get; init; } = "451 Please try again later.";
}

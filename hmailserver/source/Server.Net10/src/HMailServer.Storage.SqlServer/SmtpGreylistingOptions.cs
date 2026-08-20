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

    private long _initialRecordLifetimeTicks = TimeSpan.FromHours(24).Ticks;

    public TimeSpan InitialRecordLifetime
    {
        get => TimeSpan.FromTicks(Volatile.Read(ref _initialRecordLifetimeTicks));
        set => Volatile.Write(ref _initialRecordLifetimeTicks, value.Ticks);
    }

    private long _passedRecordLifetimeTicks = TimeSpan.FromDays(36).Ticks;

    public TimeSpan PassedRecordLifetime
    {
        get => TimeSpan.FromTicks(Volatile.Read(ref _passedRecordLifetimeTicks));
        set => Volatile.Write(ref _passedRecordLifetimeTicks, value.Ticks);
    }

    public string FailureResponse { get; init; } = "451 Please try again later.";
}

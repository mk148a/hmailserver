namespace HMailServer.Storage.SqlServer;

public sealed record SmtpGreylistingOptions
{
    public bool Enabled { get; init; }

    public bool SkipAuthenticated { get; init; } = true;

    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan InitialRecordLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan PassedRecordLifetime { get; init; } = TimeSpan.FromDays(36);

    public string FailureResponse { get; init; } = "451 Please try again later.";
}

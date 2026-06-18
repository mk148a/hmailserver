namespace HMailServer.Service;

public sealed record ExternalFetchHostedServiceOptions(
    TimeSpan PollInterval);

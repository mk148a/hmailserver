namespace HMailServer.Core.Abstractions;

public enum ExternalAccountDownloadDeleteAction
{
    UseAccountDefault = 0,
    DeleteImmediately = 1,
    DeleteAfterDays = 2,
    NeverDelete = 3
}

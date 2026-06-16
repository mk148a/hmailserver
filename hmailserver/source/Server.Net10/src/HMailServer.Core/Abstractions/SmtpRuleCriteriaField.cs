namespace HMailServer.Core.Abstractions;

public enum SmtpRuleCriteriaField
{
    Unknown = 0,
    From = 1,
    To = 2,
    Cc = 3,
    Subject = 4,
    Body = 5,
    MessageSize = 6,
    RecipientList = 7,
    DeliveryAttempts = 8
}

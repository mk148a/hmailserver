using HMailServer.Security;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class WindowsActiveDirectoryPasswordValidatorTests
{
    [TestMethod]
    public void Validate_DelegatesDomainUsernameAndPasswordWithoutRetainingThem()
    {
        string? domain = null;
        string? username = null;
        string? password = null;
        var validator = new WindowsActiveDirectoryPasswordValidator(
            (receivedDomain, receivedUsername, receivedPassword) =>
            {
                domain = receivedDomain;
                username = receivedUsername;
                password = receivedPassword;
                return true;
            });

        Assert.IsTrue(validator.Validate("CORP", "ada", "secret"));
        Assert.AreEqual("CORP", domain);
        Assert.AreEqual("ada", username);
        Assert.AreEqual("secret", password);
    }

    [TestMethod]
    public void Validate_RejectsEmptyInputsBeforeNativeBoundary()
    {
        var calls = 0;
        var validator = new WindowsActiveDirectoryPasswordValidator(
            (_, _, _) =>
            {
                calls++;
                return true;
            });

        Assert.IsFalse(validator.Validate(string.Empty, "ada", "secret"));
        Assert.IsFalse(validator.Validate("CORP", string.Empty, "secret"));
        Assert.IsFalse(validator.Validate("CORP", "ada", string.Empty));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Validate_FailsClosedWhenNativeBoundaryThrows()
    {
        var validator = new WindowsActiveDirectoryPasswordValidator(
            (_, _, _) => throw new InvalidOperationException("native failure"));

        Assert.IsFalse(validator.Validate("CORP", "ada", "secret"));
    }
}

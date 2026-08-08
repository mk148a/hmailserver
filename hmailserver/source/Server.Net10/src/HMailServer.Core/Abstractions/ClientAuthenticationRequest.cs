using System.Net;

namespace HMailServer.Core.Abstractions;

public sealed record ClientAuthenticationRequest(
    string Username,
    string Password,
    IPAddress? ClientAddress,
    ClientAuthenticationCaller Caller,
    string AuthorizationId = "");

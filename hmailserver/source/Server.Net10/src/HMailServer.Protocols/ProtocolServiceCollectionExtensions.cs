using HMailServer.Core.Abstractions;
using HMailServer.Protocols.Imap;
using HMailServer.Protocols.Pop3;
using HMailServer.Protocols.Smtp;
using Microsoft.Extensions.DependencyInjection;

namespace HMailServer.Protocols;

public static class ProtocolServiceCollectionExtensions
{
    public static IServiceCollection AddCallerAwareProtocolServices(this IServiceCollection services)
    {
        services.AddSingleton<ImapSearchCommandParser>();
        services.AddSingleton<ImapSearchExecutor>();
        services.AddSingleton<ImapSearchCommandHandler>();
        services.AddSingleton<IClientAwareAuthenticationService, ClientAwareAuthenticationService>();
        services.AddSingleton<ImapSession>();
        services.AddSingleton<ImapTcpListener>();
        services.AddSingleton<Pop3Session>();
        services.AddSingleton<Pop3TcpListener>();
        services.AddSingleton<SmtpSession>();
        services.AddSingleton<SmtpTcpListener>();
        return services;
    }
}

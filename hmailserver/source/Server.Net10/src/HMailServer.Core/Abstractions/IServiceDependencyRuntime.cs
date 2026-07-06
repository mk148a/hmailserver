namespace HMailServer.Core.Abstractions;

public interface IServiceDependencyRuntime
{
    void MakeDependent(string otherService);
}

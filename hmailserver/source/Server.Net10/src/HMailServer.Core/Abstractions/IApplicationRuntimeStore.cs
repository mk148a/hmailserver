namespace HMailServer.Core.Abstractions;

public interface IApplicationRuntimeStore
{
    ApplicationRuntimeSnapshot GetSnapshot();
}

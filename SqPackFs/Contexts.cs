using Microsoft.UI.Reactor.Core;

namespace SqPackFs;

public static class Contexts
{
    public static Context<FsService> FsService = new(new());
}

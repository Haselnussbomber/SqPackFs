using System.Diagnostics.CodeAnalysis;

namespace SqPackFs;

public static class ServiceLocator
{
    private static readonly ServiceProvider ServiceProvider;

    static ServiceLocator()
    {
        ServiceProvider = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddDebug();
            })
            .AddSqPackFs()
            .BuildServiceProvider();
    }

    public static T GetService<T>()
    {
        return ServiceProvider.GetService<T>()!;
    }

    public static bool TryGetService<T>([NotNullWhen(returnValue: true)] out T? service)
    {
        try
        {
            service = ServiceProvider.GetService<T>();
            return service != null;
        }
        catch // might catch ObjectDisposedException here
        {
            service = default;
            return false;
        }
    }

    public static void Dispose()
    {
        ServiceProvider.Dispose();
    }
}

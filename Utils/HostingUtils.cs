using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CalamityAssetBot.Utils;

public static partial class Extensions
{
    public static void AddHostedSingleton<T>(this IServiceCollection services) where T : class, IHostedService
    {
        services.AddSingleton<T>();
        services.AddHostedService<T>(provider => provider.GetService<T>()!);
    }
}
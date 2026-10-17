using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.Maui.AgeAssurance;

/// <summary>DI registration for age assurance.</summary>
public static class AgeAssuranceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAgeAssuranceProvider"/> as a singleton (idempotent). Nothing touches a
    /// native API until <see cref="IAgeAssuranceProvider.CheckAsync"/> is called.
    /// </summary>
    public static IServiceCollection AddAgeAssurance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

#if IOS
        services.TryAddSingleton<IAgeAssuranceProvider, AppleAgeAssuranceProvider>();
#elif ANDROID
        services.TryAddSingleton<IAgeAssuranceProvider, PlayAgeAssuranceProvider>();
#else
        services.TryAddSingleton<IAgeAssuranceProvider, UnavailableAgeAssuranceProvider>();
#endif

        return services;
    }
}

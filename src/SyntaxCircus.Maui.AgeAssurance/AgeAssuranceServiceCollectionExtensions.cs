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
        // TODO(native step): register the Apple Declared Age Range provider here.
        services.TryAddSingleton<IAgeAssuranceProvider, UnavailableAgeAssuranceProvider>();
#elif ANDROID
        // TODO(native step): register the Google Play Age Signals provider here.
        services.TryAddSingleton<IAgeAssuranceProvider, UnavailableAgeAssuranceProvider>();
#else
        services.TryAddSingleton<IAgeAssuranceProvider, UnavailableAgeAssuranceProvider>();
#endif

        return services;
    }
}

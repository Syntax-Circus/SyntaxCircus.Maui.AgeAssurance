using Microsoft.Extensions.DependencyInjection;

namespace SyntaxCircus.Maui.AgeAssurance.Tests;

// No direct Godot counterpart: Godot passes minimum_age through unvalidated (non-positive just yields an
// unknown eligibility, covered in AgeSignalNormalizerTests). The C# providers additionally reject 1..120
// violations; also covers the DI registration (cf. the Godot "absent plugin is unavailable" case).
public class MinimumAgeValidationTests
{
    public static TheoryData<IAgeAssuranceProvider> Providers => new()
    {
        new UnavailableAgeAssuranceProvider(),
        new FakeAgeAssuranceProvider(),
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Accepts_one_through_one_twenty(IAgeAssuranceProvider provider)
    {
        await provider.CheckAsync(1, false, TestContext.Current.CancellationToken);
        await provider.CheckAsync(120, false, TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Rejects_out_of_range_minimum_age(IAgeAssuranceProvider provider)
    {
        foreach (var bad in new[] { 0, -1, 121, int.MaxValue, int.MinValue })
        {
            await Should.ThrowAsync<ArgumentOutOfRangeException>(
                () => provider.CheckAsync(bad, false, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Rejected_call_is_not_recorded_by_the_fake()
    {
        var fake = new FakeAgeAssuranceProvider();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => fake.CheckAsync(0, true, TestContext.Current.CancellationToken));
        fake.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void AddAgeAssurance_registers_a_singleton_provider()
    {
        using var sp = new ServiceCollection().AddAgeAssurance().BuildServiceProvider();

        var a = sp.GetRequiredService<IAgeAssuranceProvider>();
        a.ShouldBeOfType<UnavailableAgeAssuranceProvider>();
        sp.GetRequiredService<IAgeAssuranceProvider>().ShouldBeSameAs(a);
    }

    [Fact]
    public void AddAgeAssurance_does_not_override_an_existing_registration()
    {
        var fake = new FakeAgeAssuranceProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IAgeAssuranceProvider>(fake);
        services.AddAgeAssurance();
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IAgeAssuranceProvider>().ShouldBeSameAs(fake);
    }

    [Fact]
    public void AddAgeAssurance_null_throws() =>
        Should.Throw<ArgumentNullException>(() => AgeAssuranceServiceCollectionExtensions.AddAgeAssurance(null!));
}

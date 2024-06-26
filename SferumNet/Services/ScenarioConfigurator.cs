using Microsoft.EntityFrameworkCore;
using SferumNet.DbModels.Common;
using SferumNet.DbModels.Scenarios;
using SferumNet.DbModels.Vk;
using SferumNet.Scenarios;
using SferumNet.Services.Common;

namespace SferumNet.Services;

public class ScenarioConfigurator : IScenarioConfigurator
{
    public DateTime? DateTimeStarted { get; set; }
    private CancellationTokenSource _cancelTokenSource = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScenarioFactory _scenarioFactory;

    public ScenarioConfigurator(IServiceScopeFactory scopeFactory, IScenarioFactory scenarioFactory)
    {
        _scopeFactory = scopeFactory;
        _scenarioFactory = scenarioFactory;
        _ = RunAsync();
    }

    public async Task RunAsync()
    {
        _cancelTokenSource = new();

        using var scope = _scopeFactory.CreateScope();
        var ef = scope.ServiceProvider.GetRequiredService<SferumNetContext>();

        var profiles = await GetProfilesAsync(ef);

        if (profiles is null)
            return;

        DateTimeStarted = DateTime.Now;

        foreach (var profile in profiles)
        {
            var scenarios = await GetScenariosByProfileAsync(ef, profile.Id);

            if (scenarios is null)
                continue;

            foreach (var scenario in scenarios)
            {
                if (scenario is Welcome)
                    await Task.Run(() =>
                        new WelcomeScenario(scope.ServiceProvider.GetRequiredService<SferumNetContext>(),
                                scope.ServiceProvider.GetRequiredService<DbLogger>(), scenario.Id)
                            .ExecuteAsync(_cancelTokenSource.Token));
                
            }
        }
    }

    public async Task StopAsync()
    {
        DateTimeStarted = null;

        await _cancelTokenSource.CancelAsync();
        _cancelTokenSource.Dispose();
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await RunAsync();
    }

    private async Task<ICollection<VkProfile>?> GetProfilesAsync(SferumNetContext ef)
    {
        return await ef.VkProfiles
            .ToListAsync(cancellationToken: _cancelTokenSource.Token);
    }

    private async Task<ICollection<Scenario>?> GetScenariosByProfileAsync(SferumNetContext ef, long idProfile)
    {
        return await ef.Scenarios
            .Where(sc => sc.IdProfile == idProfile)
            .Where(sc => sc.IsActive)
            .ToListAsync(cancellationToken: _cancelTokenSource.Token);
    }
}
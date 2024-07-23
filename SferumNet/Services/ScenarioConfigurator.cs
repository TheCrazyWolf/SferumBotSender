using Microsoft.EntityFrameworkCore;
using SferumNet.Database;
using SferumNet.DbModels.Common;
using SferumNet.DbModels.Scenarios;
using SferumNet.DbModels.Vk;
using SferumNet.Scenarios;
using SferumNet.Scenarios.Common;
using SferumNet.Services.Common;
using WelcomeJob = SferumNet.DbModels.Scenarios.WelcomeJob;
// ReSharper disable AsyncVoidLambda

namespace SferumNet.Services;

public class ScenarioConfigurator : IScenarioConfigurator
{
    public DateTime? DateTimeStarted { get; set; }
    private CancellationTokenSource _cancelTokenSource = new();
    private readonly IServiceScopeFactory _scopeFactory;

    private List<Thread> _threads = new();

    public ScenarioConfigurator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        
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
            var scenarios = await GetJobsByProfileAsync(ef, profile.Id);

            if (scenarios is null)
                continue;

            foreach (var scenario in scenarios)
            {
                var thread = scenario switch
                {
                    FactJob => new Thread(async () => await JobFactory<FactsJob>(scenario.Id).ExecuteAsync(_cancelTokenSource.Token)),
                    ScheduleJob => new Thread(async () => await JobFactory<SchedulesJob>(scenario.Id).ExecuteAsync(_cancelTokenSource.Token)),
                    WelcomeJob => new Thread(async () => await JobFactory<WelcomesJob>(scenario.Id).ExecuteAsync(_cancelTokenSource.Token)),
                    _ => throw new ArgumentOutOfRangeException(nameof(scenario))
                };
                thread.Name = $"{scenario.Id}";
                thread.Start();
                _threads.Add(thread);

                // ETC ...
            }
        }
    }

    private BaseJob JobFactory<T>(long scenarioId) where T : BaseJob
    {
        return (T)Activator.CreateInstance(typeof(T), _scopeFactory, scenarioId)!;
    }

    public async Task StopAsync()
    {
        DateTimeStarted = null;

        await _cancelTokenSource.CancelAsync();
        foreach (var thread in _threads)
        {
            thread.Interrupt();
        }

        _threads = new();
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

    private async Task<ICollection<Job>?> GetJobsByProfileAsync(SferumNetContext ef, long idProfile)
    {
        return await ef.Scenarios
            .Where(sc => sc.IdProfile == idProfile)
            .Where(sc => sc.IsActive)
            .ToListAsync(cancellationToken: _cancelTokenSource.Token);
    }
}
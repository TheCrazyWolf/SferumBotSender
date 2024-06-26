using Microsoft.EntityFrameworkCore;
using SferumNet.DbModels.Common;
using SferumNet.DbModels.Enum;
using SferumNet.DbModels.Vk;
using SferumNet.Services;
using VkNet;
using VkNet.Infrastructure;
using VkNet.Model;

namespace SferumNet.Scenarios.Common;

public class BaseScenario : IScenario
{
    protected readonly long IdScenario;
    protected CancellationToken CancellationToken;
    
    /* Services */
    protected readonly DbLogger Logger;
    private readonly VkFactory _vkFactory;
    
    protected readonly SferumNetContext Ef;
    protected readonly VkApi VkApi; 
    
    protected VkProfile? _currentProfileDb;
    protected Scenario? _currentScDb;
    
    public BaseScenario(SferumNetContext ef, DbLogger dbLogger, long idScenario)
    {
        Ef = ef;
        Logger = dbLogger;
        IdScenario = idScenario;

        VkApi = new VkApi();
        _vkFactory = new VkFactory();
    }
    
    public virtual Task ExecuteAsync(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public virtual bool CanBeExecuted()
    {
        return false;
    }

    public virtual Task ProcessAsync()
    {
        return Task.CompletedTask;
    }

    private async Task<VkProfile?> GetProfileAsync(long? idProfile)
    {
        return await Ef.VkProfiles
            .FirstOrDefaultAsync(x => x.Id == idProfile, cancellationToken: CancellationToken);
    }

    private async Task<Scenario?> GetScenarioAsync(long idSc)
    {
        return await Ef.Scenarios
            .FirstOrDefaultAsync(x => x.Id == idSc, cancellationToken: CancellationToken);
    }

    protected async Task UpdateProfileAndScAsync()
    {
        _currentScDb = await GetScenarioAsync(IdScenario);

        if (_currentScDb is null)
            return;
        
        _currentProfileDb = await GetProfileAsync(_currentScDb.IdProfile);
    }

    protected async Task RefreshIfTokenExpireAsync()
    {
        if (_currentProfileDb is null)
            return;
        
        if (_currentProfileDb.AccessTokenExpired <= DateTime.Now.Ticks)
        {
            await ConfigureVkApiAsync();
            
            await Logger.LogAsync(IdScenario, EventType.Info, "Срок действия токена истек. Запрашиваем новый");
            
            var accounts = await _vkFactory.GetAccountsAsync(_currentProfileDb.RemixSid);

            var webTokenAccount = accounts?.FirstOrDefault(x => x.UserId == _currentProfileDb.UserId);

            if (webTokenAccount is null)
            {
                await Logger.LogAsync(IdScenario, EventType.Error, "Не удалось обновить токен.");
                return;
            }

            _currentProfileDb.AccessTokenExpired = webTokenAccount.Expires;
            _currentProfileDb.UserId = webTokenAccount.UserId;
            _currentProfileDb.AccessToken = webTokenAccount.AccessToken;

            Ef.Update(_currentProfileDb);
            await Ef.SaveChangesAsync(CancellationToken);
        }
    }

    protected async Task ResetCounterExecutedIfNextDayAsync()
    {
        if(_currentScDb is null)
            return;

        if (_currentScDb.LastExecuted.Date != DateTime.Today.Date)
        {
            await Logger.LogAsync(IdScenario, EventType.Info, "День прошел. Сбрасываем счётчик");
            _currentScDb.TotalExecuted = 0;
            Ef.Update(_currentScDb);
            await Ef.SaveChangesAsync(CancellationToken);
        }
    }


    protected async Task ProccessInrecementExecutedAsync()
    {
        if(_currentScDb is null)
            return;
        
        _currentScDb.TotalExecuted++;
        _currentScDb.LastExecuted = DateTime.Now;
        Ef.Update(_currentScDb);
        await Ef.SaveChangesAsync(CancellationToken);
        
        await Logger.LogAsync(IdScenario, EventType.Success, $"Сценарий успешно выполнен");
    }

    private async Task ConfigureVkApiAsync()
    {
        VkApi.VkApiVersion.SetVersion(5,226);

        if (_currentProfileDb is null)
            return;

        await VkApi.AuthorizeAsync(new ApiAuthParams
        {
            AccessToken = _currentProfileDb.AccessToken
        }, CancellationToken);
    }
}
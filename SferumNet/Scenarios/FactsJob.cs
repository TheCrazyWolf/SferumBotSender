using Microsoft.EntityFrameworkCore;
using SferumNet.Database;
using SferumNet.DbModels.Scenarios;
using SferumNet.Services;

namespace SferumNet.Scenarios;

public class FactsJob : WelcomeJob
{
    public FactsJob(SferumNetContext ef, DbLogger dbLogger, long idScenario) : base(ef, dbLogger, idScenario)
    {
    }

    protected override async Task<string> GetSentencesAsync()
    {
        var countTotal = await Ef.FactsSentences.CountAsync();
        var randomIndex = new Random().Next(countTotal);
            
        var thisSentence = await Ef.FactsSentences
            .OrderBy(w => w.Id) 
            .Skip(randomIndex)
            .FirstOrDefaultAsync();

        if (thisSentence is null)
            return $"База данных предложений не заполнена {new Random().Next()}";

        if (CurrentJob is FactJob jobUpcast)
        {
            if (jobUpcast.IsShuffleWords)
                return ShuffleWords(thisSentence.Message);
        }

        return thisSentence.Message;
    }

    private string ShuffleWords(string sentence)
    {
        List<string> words = sentence.Split(' ').ToList();
        words = words.OrderBy(x => Guid.NewGuid()).ToList(); 
        return string.Join(" ", words);
    }
}
using System.Threading.Tasks;

namespace BetterGenshinImpact.Service.Interface;

public interface IGameLanguageService
{
    bool SetGameLanguage(int langId);
    Task<bool> SetGameLanguageAsync(int langId);
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BetterGenshinImpact.Genshin.Settings;
using BetterGenshinImpact.Genshin.Settings2;
using Microsoft.Win32;
using Newtonsoft.Json;

using BetterGenshinImpact.Service.Interface;
using System.Threading.Tasks;

namespace BetterGenshinImpact.Service;

public class GameLanguageService : IGameLanguageService
{
    public const int SimplifiedChineseType = 2;
    public const int JapaneseType = 9;

    public Task<bool> SetGameLanguageAsync(int langId)
    {
        return Task.FromResult(SetGameLanguage(langId));
    }

    public bool SetGameLanguage(int langId)
    {
        try
        {
            var key = GenshinRegistry.GetRegistryKey();
            if (key == null) return false;

            using (key)
            {
                string valueName = SearchRegistryName(key);
                if (key.GetValue(valueName) is not byte[] rawBytes)
                {
                    return false;
                }

                // Parse the existing JSON data
                string json;
                unsafe
                {
                    fixed (byte* ptr = rawBytes)
                    {
                        json = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(ptr));
                    }
                }

                // Use Newtonsoft.Json for compatibility with existing classes
                var settings = JsonConvert.DeserializeObject<GenshinGameSettings>(json);
                if (settings == null) return false;

                // Update language
                settings.DeviceLanguageType = langId;

                // Serialize back to JSON
                string updatedJson = JsonConvert.SerializeObject(settings);
                byte[] updatedBytes = Encoding.UTF8.GetBytes(updatedJson + "\0");

                // Write back to registry
                key.SetValue(valueName, updatedBytes);
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            return false;
        }
    }

    private static string SearchRegistryName(RegistryKey key)
    {
        string value_name = string.Empty;
        string[] names = key.GetValueNames();

        foreach (string name in names)
        {
            if (name.Contains("GENERAL_DATA"))
            {
                value_name = name;
                break;
            }
        }

        if (value_name == string.Empty)
        {
            throw new ArgumentException("GENERAL_DATA value not found in registry.");
        }

        return value_name;
    }
}

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherWhisper;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "cump.whispermod";
    public const string ModName = "CasualtiesTogetherWhisper";
    public const string ModVersion = "0.0.5";

    internal static new ManualLogSource Logger;
    
    private readonly Harmony _harmony = new(ModGuid);
    public static Plugin Instance { get; private set; } = null!;
    
    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;
        
        gameObject.AddComponent<IconController>();

        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {ModName} is loaded!");
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    private void Update()
    {
        if (!KrokoshaScavMultiplayer.network_system_is_running)
        {
            NetworkController.ResetSession();
        }
        else
        {
            NetworkController.EnsureHandlers();
        }
    }
    
    public static bool HasWhisperCommand(string message)
    {
        if (!message.StartsWith("/w"))
            return false;
        if (message.Length == 2)
            return true;
        if (message[2] == ' ' || (message[2] >= '0' && message[2] <= '9'))
            return true;
        return false;
    }

    public static bool ParseWhisperCommand(string input, out int hearingRange, out string message)
    {
        if (!HasWhisperCommand(input))
        {
            hearingRange = 0;
            message = null;
            return false;
        }
        
        input = input.Remove(0, 2);
        if (input.Length == 0)
        {
            hearingRange = 20;
            message = "";
            return true;
        }
        else if (input[0] == ' ')
        {
            hearingRange = 20;
            message = input.Trim();
            return true;
        }
        else
        {
            int i = 0;
            while (i < input.Length && input[i] >= '0' && input[i] <= '9')
                i++;
            if (int.TryParse(input.Substring(0, i), out int range))
            {
                hearingRange = range;
                message = input.Remove(0, i).TrimStart();
                return true;
            }
            else
            {
                hearingRange = 20;
                message = "";
                return false;
            }
        }
    }

    public static bool IsValidHearingRange(int hearingRange) => hearingRange is > 0 and <= 40;
}


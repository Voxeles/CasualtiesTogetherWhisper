using HarmonyLib;
using KrokoshaCasualtiesMP;

namespace CasualtiesTogetherWhisper;

internal static class Patches
{
    [HarmonyPatch(typeof(Chat))]
    internal static class ChatPatch
    {
        [HarmonyPatch(nameof(Chat.OnEnteredUserMessage))]
        [HarmonyPrefix]
        private static bool OnEnteredUserMessagePrefix(Chat __instance)
        {
            var input = Chat.CHAT_current_input.TrimEnd();
            if (!Plugin.HasWhisperCommand(input))
                return true; // Continue parsing
            
            NetworkController.SendWhisper(input);
            
            Chat._chatinput_changed = true;
            if (!Chat.MyMessageLog.Contains(in Chat.CHAT_current_input))
                Chat.MyMessageLog.Enqueue(Chat.CHAT_current_input);
            Chat.MyMessageLog_Index = Chat.MyMessageLog.Count;
            Chat.CHAT_current_input = "";
            return false;
        }
    }
}


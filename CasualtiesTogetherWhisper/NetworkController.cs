using System;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using LiteNetLib;
using LiteNetLib.Utils;

namespace CasualtiesTogetherWhisper;

public static class NetworkController
{
    private const ushort ClientWhisperMsg = 48999;

    private static bool _handlersRegistered = false;

    public static void ResetSession()
    {
        _handlersRegistered = false;
    }

    public static void EnsureHandlers()
    {
        if (!KrokoshaScavMultiplayer.network_system_is_running || _handlersRegistered)
            return;
        Net.RegisterServerReceiver(ClientWhisperMsg, OnServerWhisper);
        _handlersRegistered = true;
    }

    public static void SendWhisper(string command)
    {
        if (command == null)
            return;
        Plugin.ParseWhisperCommand(command, out int hearingRange, out string message);
        if (!Plugin.IsValidHearingRange(hearingRange))
        {
            Chat.LogMessage("*SYSTEM*", "Range is invalid.");
            return;
        }
        if (message == null || !Chat.ValidateChatMessage(in message))
        {
            Chat.LogMessage("*SYSTEM*", "Message is invalid.");
            return;
        }
        if (!Net.running)
        {
            Chat.LogMessage("*OFFLINE*", message);
            return;
        }

        var writer = Net.CreateWriter(ClientWhisperMsg);
        writer.Put(hearingRange);
        writer.Put(message);
        Net.Client_Send(DeliveryMethod.ReliableUnordered, writer);
    }

    private static void OnServerWhisper(knetid clientId, ref NetDataReader reader)
    {
        reader.Get(out int hearingRange);
        if (!Plugin.IsValidHearingRange(hearingRange))
        {
            NetDataWriter writer = Net.CreateWriter(10098);
            writer.Put((byte) 1);
            writer.Put("Hearing range is invalid!");
            Net.Server_SendToClients(DeliveryMethod.Unreliable, writer, clientId);
            return;
        }
        
        reader.Get(out string message);
        if (string.IsNullOrWhiteSpace(message) || !NetPlayer.TryGetPlayerFromClientId(clientId, out NetPlayer sender))
            return;
        if (!Util.IsWorldGenerated())
        {
            NetDataWriter writer = Net.CreateWriter(10098);
            writer.Put((byte) 1);
            writer.Put("Cannot send a whisper on the main menu!");
            Net.Server_SendToClients(DeliveryMethod.Unreliable, writer, clientId);
            return;
        }
        if (sender.server_mute_tc)
        {
            NetDataWriter writer = Net.CreateWriter(10098);
            writer.Put((byte) 1);
            writer.Put("You're muted by the server!");
            Net.Server_SendToClients(DeliveryMethod.Unreliable, writer, clientId);
            return;
        }
        if (!Chat.ValidateChatMessage(message))
        {
            NetDataWriter writer = Net.CreateWriter(10098);
            writer.Put((byte) 1);
            writer.Put("Invalid chat message!");
            Net.Server_SendToClients(DeliveryMethod.Unreliable, writer, clientId);
            return;
        }
        
        if (Chat.SHOULD_LOG_CHAT)
            Plugin.Logger.LogInfo($"SERVER: \"{sender}\" WHISPER MESSAGE: {message}");
        try
        {
            var onPlayerChatMessage = AccessTools.Field(typeof(Chat), nameof(Chat.OnPlayerChatMessage))?.GetValue(null) as Action<NetPlayer, string>;
            onPlayerChatMessage?.Invoke(sender, message);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("SERVER: OnPlayerChatMessage: " + ex);
        }
        
        if (sender.IsAlive() && KrokoshaScavMultiplayer.rules.SpeechImpairedChat)
            message = sender.body.talker.DistortString(message);
        
        var chatTag = "whisper";
        
        foreach (var player in NetPlayer.GetPlayersInRadius(sender.body.GetPosition(), hearingRange))
        {
            var ownMessage = message;
            var ownChatTag = chatTag;
            if (player != sender)
            {
                if (!sender.CanCommunicateWith_TextChat(player))
                    continue;
                if (player.IsAlive())
                    player.playerbody.HearinglossDistortMessage(sender.playerbody, ref ownMessage, ref ownChatTag);
                if (string.IsNullOrWhiteSpace(ownMessage))
                    continue;
            }
            
            var writer = Net.CreateWriter(10098);
            writer.Put((byte)0);
            writer.Put(clientId);
            writer.Put(ownChatTag);
            writer.Put(ownMessage);
            Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, writer, player.clientId);
        }
    }
}
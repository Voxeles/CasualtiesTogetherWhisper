using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using UnityEngine;

namespace CasualtiesTogetherWhisper;

public class IconController : MonoBehaviour
{
    private static Texture2D _iconTexture;
    private Dictionary<NetPlayer, GameObject> _playerIcons = [];
    private bool _wasTyping = false;
    private float _timer = 0;
    private int _attempts = 0;
    
    private void Awake()
    {
        LoadIconTexture();
        WorldgenPatches.OnWorldgenFinish += OnWorldgenFinish;
    }

    private void OnDestroy()
    {
        WorldgenPatches.OnWorldgenFinish -= OnWorldgenFinish;
        foreach (var icon in _playerIcons.Values)
            Destroy(icon);
        _playerIcons.Clear();
    }

    private void OnWorldgenFinish()
    {
        foreach (var icon in _playerIcons.Values)
            Destroy(icon);
        _playerIcons.Clear();
        foreach (var player in NetPlayer.ClientIdToPlayerDict.Values)
            _playerIcons.Add(player, CreateIconObjectForPlayer(player));
        _attempts = 0;
        _timer = 0;
    }
    
    private static GameObject CreateIconObjectForPlayer(NetPlayer player)
    {
        if (!Util.IsWorldGenerated())
        {
            Plugin.Logger.LogError("Do not call CreateIconObjectForPlayer before world is created!");
            return null;
        }
        var icon = new GameObject($"{player.playername}InRangeOfWhisperIcon");
        icon.transform.parent = player.body.transform;
        icon.transform.localScale = Vector3.one * 5f;
        var sprRenderer = icon.AddComponent<SpriteRenderer>();
        sprRenderer.sortingOrder = 6001;
        sprRenderer.color = player.plrcolor;
        sprRenderer.sprite = Sprite.Create(_iconTexture, new Rect(0, 0, _iconTexture.width, _iconTexture.height), new Vector2(0.5f, 0.5f));
        icon.SetActive(false);
        return icon;
    }

    private void LateUpdate()
    {
        if (!Util.IsWorldGenerated())
            return;

        if (NetPlayer.ClientIdToPlayerDict.Count != _playerIcons.Count)
        {
            if (!TryCreateIconObjects())
                return;
        }
        
        if (!Chat.CHAT_textbox_input_focused
            || !Plugin.HasWhisperCommand(Chat.CHAT_current_input)
            || !Plugin.ParseWhisperCommand(Chat.CHAT_current_input, out int hearingRange, out _)
            || !Plugin.IsValidHearingRange(hearingRange))
        {
            if (!_wasTyping)
                return;

            foreach (var icon in _playerIcons.Values)
                icon.SetActive(false);
            
            _wasTyping = false;
            return;
        }
        
        _wasTyping = true;

        var pos = NetPlayer.LOCAL_PLAYER.body.GetPosition();
        var radius = hearingRange * hearingRange;
        foreach (var player in NetPlayer.ClientIdToPlayerDict.Values)
        {
            if (!player.body || !player.body.alive || player.is_local)
                continue;
            var headPos = player.body.GetHead().transform.position;

            var icon = _playerIcons[player];
            var position = icon.transform.position;
            var y = position.y;

            bool inRadius = KM.dist2dsqrcheck_presqr(player.pos, in pos, radius);
            if (!inRadius)
            {
                if (!icon.activeSelf)
                    continue;
                var leavePos = headPos + Vector3.up * 4f;
                position = Vector3.Lerp(position, leavePos, Time.deltaTime * 30f);
                position.y = Mathf.Lerp(y, leavePos.y, Time.deltaTime * 10f);
                icon.transform.position = position;
                if (Mathf.Abs(position.y - leavePos.y) <= 1f)
                    icon.SetActive(false);
            }
            else
            {
                if (!icon.activeSelf)
                {
                    position = headPos + Vector3.up * 4f;
                    y = position.y;
                    icon.SetActive(true);
                }
                var desired = headPos + Vector3.up * 1.5f;
                position = Vector3.Lerp(position, desired, Time.deltaTime * 30f);
                position.y = Mathf.Lerp(y, desired.y, Time.deltaTime * 5f);
                icon.transform.position = position;
            }
        }
    }

    private static void LoadIconTexture()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        const string assetName = "CasualtiesTogetherWhisper.assets.in_hearing_range.png";
        byte[] assetBytes;
        using (Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(assetName))
        {
            if (manifestResourceStream == null)
            {
                Plugin.Logger.LogError($"Failed to load asset {assetName}!!!");
                return;
            }
            assetBytes = new byte[manifestResourceStream.Length];
            manifestResourceStream.Read(assetBytes, 0, assetBytes.Length);
        }
        
        _iconTexture = new Texture2D(2, 2);
        _iconTexture.LoadImage(assetBytes);
        _iconTexture.filterMode = FilterMode.Point;
    }

    private bool TryCreateIconObjects()
    {
        _timer += Time.deltaTime;
        
        if (_timer > 10.0f && _attempts < 10)
        {
            Plugin.Logger.LogWarning("Hacking my way around the body-player desync! Here be dragons!!!");
            _attempts += 1;
            
            var bodies = FindObjectsByType<Body>(FindObjectsSortMode.None);
            var netBodies = FindObjectsByType<NetBody>(FindObjectsSortMode.None);
            Plugin.Logger.LogInfo($"BODIES: {bodies.Length} NETBODIES: {netBodies.Length}");
            if (bodies.Length != netBodies.Length)
            {
                Plugin.Logger.LogWarning($"Bodies and netBodies doesn't match!");
                return false;
            }
            foreach (var body in netBodies)
            {
                if (!body.player)
                {
                    Plugin.Logger.LogWarning($"{body.name} doesn't have a player!");
                    continue;
                }
                if (NetPlayer.BodyToPlayerDict.ContainsKey(body.body))
                    continue;
                NetPlayer.BodyToPlayerDict.Add(body.body, body.player);
                body.player.body = body.body;
                Plugin.Logger.LogInfo($"Fixed player {body.player.playername}");
            }

            _timer = 0;
        }

        var failedAtLeastOnce = false;
        foreach (var player in NetPlayer.ClientIdToPlayerDict.Values)
        {
            if (_playerIcons.ContainsKey(player))
                continue;
            if (!player.TryGetNetBody(out _))
            {
                failedAtLeastOnce = true;
                continue;
            }
            _playerIcons.Add(player, CreateIconObjectForPlayer(player));
            _timer = 0;
        }

        List<NetPlayer> toRemove = [];
        foreach (var pair in _playerIcons)
        {
            if (NetPlayer.ClientIdToPlayerDict.ContainsValue(pair.Key))
                continue;
            toRemove.Add(pair.Key);
        }
        foreach (var player in toRemove)
        {
            Destroy(_playerIcons[player]);
            _playerIcons.Remove(player);
        }

        return !failedAtLeastOnce;
    }
}
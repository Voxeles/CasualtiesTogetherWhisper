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
    private Dictionary<NetBody, GameObject> _playerIcons = [];
    private bool _wasTyping = false;
    private float _timer = 0;

    private void Awake()
    {
        LoadIconTexture();
        WorldgenPatches.OnWorldgenFinish += OnWorldgenFinish;
        NetPlayer.OnPlayerLeft += OnPlayerLeft;
    }

    private void OnDestroy()
    {
        WorldgenPatches.OnWorldgenFinish -= OnWorldgenFinish;
        NetPlayer.OnPlayerLeft -= OnPlayerLeft;
        foreach (var icon in _playerIcons.Values)
            Destroy(icon);
        _playerIcons.Clear();
    }

    private void OnWorldgenFinish()
    {
        foreach (var icon in _playerIcons.Values)
            Destroy(icon);
        _playerIcons.Clear();
    }

    private void OnPlayerLeft(NetPlayer player)
    {
        if (!Util.IsWorldGenerated())
            return;

        foreach (var netBody in NetBody.all_instances)
        {
            if (netBody?.player == null || player != netBody.player)
                continue;
            if (!_playerIcons.TryGetValue(netBody, out var icon))
                continue;
            Destroy(icon);
            _playerIcons.Remove(netBody);
        }
    }

    private void LateUpdate()
    {
        if (!Util.IsWorldGenerated())
            return;

        EnsureIconObjects();

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

        var pos = NetPlayer.LOCAL_PLAYER.pos;
        var radius = hearingRange * hearingRange;
        foreach (var netBody in NetBody.all_instances)
        {
            if (netBody == null || netBody.body == null || !netBody.body.alive || netBody.is_local)
                continue;
            if (!_playerIcons.TryGetValue(netBody, out var icon))
                continue;
            var headPos = netBody.body.GetHead().transform.position;
            var position = icon.transform.position;
            var y = position.y;

            bool inRadius = KM.dist2dsqrcheck_presqr(netBody.pos, in pos, radius);
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

    private void EnsureIconObjects()
    {
        if (NetPlayer.ClientIdToPlayerDict.Count == _playerIcons.Count)
            return;

        _timer += Time.deltaTime;
        if (_timer < 2.0f)
            return;
        _timer = 0;

        foreach (var netBody in NetBody.all_instances)
        {
            if (netBody == null)
                continue;

            _playerIcons.TryGetValue(netBody, out var icon);
            if (icon != null)
                continue;

            _playerIcons.Remove(netBody);
            icon = CreateIconObjectForPlayer(netBody);
            if (icon != null)
                _playerIcons.Add(netBody, icon);
        }
    }

    private static GameObject CreateIconObjectForPlayer(NetBody netBody)
    {
        if (!Util.IsWorldGenerated() || !netBody.player || !netBody.body)
            return null;
        var icon = new GameObject($"{netBody.playername}InRangeOfWhisperIcon");
        icon.transform.parent = netBody.body.transform;
        icon.transform.localScale = Vector3.one * 5f;
        var sprRenderer = icon.AddComponent<SpriteRenderer>();
        sprRenderer.sortingOrder = 6001;
        if (Plugin.MpModPlayerColorField != null)
            sprRenderer.color = (Color24)Plugin.MpModPlayerColorField.GetValue(netBody.player);
        else
            sprRenderer.color = Color.white;
        sprRenderer.sprite = Sprite.Create(_iconTexture, new Rect(0, 0, _iconTexture.width, _iconTexture.height), new Vector2(0.5f, 0.5f));
        icon.SetActive(false);
        return icon;
    }
}

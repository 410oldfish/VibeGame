using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexNetworkSessionController : MonoBehaviour
    {
        private readonly HexAuthoritativeCommandQueue _hostCommandQueue = new();
        private readonly List<HexPlayerLobbyState> _players = new();

        private HexNetworkMode _mode = HexNetworkMode.Offline;
        private HexRoomSettings _roomSettings = new();
        private string _localPlayerId = "local-player";

        public event Action LobbyChanged;
        public event Action<HexNetworkCommand> HostCommandAccepted;

        public HexNetworkMode Mode => _mode;
        public HexRoomSettings RoomSettings => _roomSettings;
        public IReadOnlyList<HexPlayerLobbyState> Players => _players;
        public string LocalPlayerId => _localPlayerId;
        public bool IsOffline => _mode == HexNetworkMode.Offline;
        public bool IsHostAuthority => _mode == HexNetworkMode.Offline || _mode == HexNetworkMode.Host;

        public HexPlayerLobbyState LocalPlayer => GetOrCreatePlayer(_localPlayerId, _mode != HexNetworkMode.Client);

        public static HexNetworkSessionController Instance { get; private set; }

        public static HexNetworkSessionController EnsureExists()
        {
            if (Instance != null)
                return Instance;

            Instance = FindFirstObjectByType<HexNetworkSessionController>();
            if (Instance != null)
                return Instance;

            var go = new GameObject(nameof(HexNetworkSessionController));
            Instance = go.AddComponent<HexNetworkSessionController>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureOffline();
        }

        public void ConfigureOffline()
        {
            _mode = HexNetworkMode.Offline;
            _roomSettings = new HexRoomSettings
            {
                roomName = "本地冒险",
                roomCode = "LOCAL",
                visibility = HexRoomVisibility.Private,
                maxPlayers = 1,
            };
            _hostCommandQueue.Clear();
            _players.Clear();
            var player = GetOrCreatePlayer(_localPlayerId, true);
            player.displayName = "玩家";
            player.isReady = false;
            player.hasSelectedProfession = false;
            NotifyLobbyChanged();
        }

        public void CreateHostRoom(HexRoomSettings settings)
        {
            _mode = HexNetworkMode.Host;
            _roomSettings = settings ?? new HexRoomSettings();
            if (string.IsNullOrWhiteSpace(_roomSettings.roomCode))
                _roomSettings.roomCode = GenerateLocalRoomCode();

            _hostCommandQueue.Clear();
            _players.Clear();
            var player = GetOrCreatePlayer(_localPlayerId, true);
            player.displayName = "主机";
            player.isReady = false;
            player.hasSelectedProfession = false;
            NotifyLobbyChanged();
        }

        public void JoinRoomByCode(string roomCode, string password)
        {
            _mode = HexNetworkMode.Client;
            _roomSettings = new HexRoomSettings
            {
                roomName = "远程房间",
                roomCode = roomCode ?? string.Empty,
                password = password ?? string.Empty,
                visibility = HexRoomVisibility.Private,
                maxPlayers = 3,
            };
            _players.Clear();
            var player = GetOrCreatePlayer(_localPlayerId, false);
            player.displayName = "玩家";
            NotifyLobbyChanged();
        }

        public HexNetworkCommand SelectLocalProfession(HexCardProfession profession)
        {
            var player = LocalPlayer;
            player.profession = profession;
            player.hasSelectedProfession = true;

            var payload = JsonUtility.ToJson(new HexProfessionPayload { profession = profession });
            var command = SubmitLocalCommand(HexNetworkCommandType.SelectProfession, payload);
            NotifyLobbyChanged();
            return command;
        }

        public HexNetworkCommand ConfirmLocalReady()
        {
            var player = LocalPlayer;
            player.isReady = player.hasSelectedProfession;
            var command = SubmitLocalCommand(HexNetworkCommandType.ConfirmReady, string.Empty);
            NotifyLobbyChanged();
            return command;
        }

        public bool CanHostStartRun()
        {
            if (!IsHostAuthority || _players.Count == 0)
                return false;

            for (int i = 0; i < _players.Count; i++)
            {
                if (!_players[i].hasSelectedProfession || !_players[i].isReady)
                    return false;
            }

            return true;
        }

        public HexNetworkCommand SubmitLocalCommand(HexNetworkCommandType commandType, string payloadJson)
        {
            if (IsHostAuthority)
            {
                var command = _hostCommandQueue.Accept(_localPlayerId, commandType, payloadJson);
                HostCommandAccepted?.Invoke(command);
                return command;
            }

            // Client mode will send this through a Mirror Command after transport is wired.
            return new HexNetworkCommand
            {
                sequence = 0,
                senderPlayerId = _localPlayerId,
                commandType = commandType,
                payloadJson = payloadJson ?? string.Empty,
                submittedAt = Time.realtimeSinceStartupAsDouble,
            };
        }

        public bool TryDequeueHostCommand(out HexNetworkCommand command)
        {
            return _hostCommandQueue.TryDequeue(out command);
        }

        private HexPlayerLobbyState GetOrCreatePlayer(string playerId, bool isHost)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].playerId == playerId)
                    return _players[i];
            }

            var player = new HexPlayerLobbyState
            {
                playerId = playerId,
                displayName = playerId,
                isHost = isHost,
            };
            _players.Add(player);
            return player;
        }

        private void NotifyLobbyChanged()
        {
            LobbyChanged?.Invoke();
        }

        private static string GenerateLocalRoomCode()
        {
            return UnityEngine.Random.Range(100000, 999999).ToString();
        }
    }
}

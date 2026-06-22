using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexDemo
{
    public sealed class HexNetworkSessionController : MonoBehaviour
    {
        public event Action LobbyChanged
        {
            add => HexGameModule.Network.LobbyChanged += value;
            remove => HexGameModule.Network.LobbyChanged -= value;
        }

        public event Action<HexNetworkCommand> HostCommandAccepted
        {
            add => HexGameModule.Network.HostCommandAccepted += value;
            remove => HexGameModule.Network.HostCommandAccepted -= value;
        }

        public HexNetworkMode Mode => HexGameModule.Network.Mode;
        public HexRoomSettings RoomSettings => HexGameModule.Network.RoomSettings;
        public IReadOnlyList<HexPlayerLobbyState> Players => HexGameModule.Network.Players;
        public string LocalPlayerId => HexGameModule.Network.LocalPlayerId;
        public bool IsOffline => HexGameModule.Network.IsOffline;
        public bool IsHostAuthority => HexGameModule.Network.IsHostAuthority;
        public HexPlayerLobbyState LocalPlayer => HexGameModule.Network.LocalPlayer;

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
            HexGameModule.Initialize();
        }

        public void ConfigureOffline()
        {
            HexGameModule.Network.ConfigureOffline();
        }

        public void CreateHostRoom(HexRoomSettings settings)
        {
            HexGameModule.Network.CreateHostRoom(settings);
        }

        public void JoinRoomByCode(string roomCode, string password)
        {
            HexGameModule.Network.JoinRoomByCode(roomCode, password);
        }

        public HexNetworkCommand SelectLocalProfession(HexCardProfession profession)
        {
            return HexGameModule.Network.SelectLocalProfession(profession);
        }

        public HexNetworkCommand ConfirmLocalReady()
        {
            return HexGameModule.Network.ConfirmLocalReady();
        }

        public bool CanHostStartRun()
        {
            return HexGameModule.Network.CanHostStartRun();
        }

        public HexNetworkCommand SubmitLocalCommand(HexNetworkCommandType commandType, string payloadJson)
        {
            return HexGameModule.Network.SubmitLocalCommand(commandType, payloadJson);
        }

        public bool TryDequeueHostCommand(out HexNetworkCommand command)
        {
            return HexGameModule.Network.TryDequeueHostCommand(out command);
        }
    }
}

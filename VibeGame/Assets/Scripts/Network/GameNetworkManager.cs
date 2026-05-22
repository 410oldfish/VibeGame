using UnityEngine;

#if HEX_MIRROR_PRESENT
using Mirror;
#endif

namespace HexDemo.Network
{
#if HEX_MIRROR_PRESENT
    public sealed class GameNetworkManager : NetworkManager
#else
    public sealed class GameNetworkManager : MonoBehaviour
#endif
    {
        public const ushort DefaultKcpPort = 7777;

        [SerializeField] private string directConnectAddress = "localhost";
        [SerializeField] private ushort kcpPort = DefaultKcpPort;

        public string DirectConnectAddress
        {
            get => directConnectAddress;
            set => directConnectAddress = string.IsNullOrWhiteSpace(value) ? "localhost" : value;
        }

        public ushort KcpPort
        {
            get => kcpPort;
            set => kcpPort = value == 0 ? DefaultKcpPort : value;
        }

        public static GameNetworkManager EnsureExists()
        {
            var existing = FindFirstObjectByType<GameNetworkManager>();
            if (existing != null)
                return existing;

            var go = new GameObject(nameof(GameNetworkManager));
            return go.AddComponent<GameNetworkManager>();
        }

        public void StartLanHost()
        {
#if HEX_MIRROR_PRESENT
            ConfigureMirrorAddress();
            StartHost();
#else
            HexNetworkSessionController.EnsureExists().CreateHostRoom(new HexRoomSettings
            {
                roomName = "LAN Host",
                roomCode = directConnectAddress,
                visibility = HexRoomVisibility.Private,
                maxPlayers = 3,
            });
            Debug.Log("Mirror is not imported yet. LAN host request stored in local session only.");
#endif
        }

        public void StartLanClient(string hostAddress)
        {
            DirectConnectAddress = hostAddress;
#if HEX_MIRROR_PRESENT
            ConfigureMirrorAddress();
            StartClient();
#else
            HexNetworkSessionController.EnsureExists().JoinRoomByCode(directConnectAddress, string.Empty);
            Debug.Log("Mirror is not imported yet. LAN client request stored in local session only.");
#endif
        }

        public void StopLanSession()
        {
#if HEX_MIRROR_PRESENT
            if (NetworkServer.active && NetworkClient.isConnected)
                StopHost();
            else if (NetworkClient.isConnected)
                StopClient();
            else if (NetworkServer.active)
                StopServer();
#else
            HexNetworkSessionController.EnsureExists().ConfigureOffline();
#endif
        }

#if HEX_MIRROR_PRESENT
        private void ConfigureMirrorAddress()
        {
            networkAddress = directConnectAddress;
        }
#endif
    }
}

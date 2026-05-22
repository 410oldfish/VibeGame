using System.Collections.Generic;
using HexDemo;
using UnityEngine;

#if HEX_MIRROR_PRESENT
using Mirror;
#endif

namespace HexDemo.Network
{
#if HEX_MIRROR_PRESENT
    public sealed class LobbyNetworkController : NetworkBehaviour
#else
    public sealed class LobbyNetworkController : MonoBehaviour
#endif
    {
        private readonly List<HexLobbyPlayerMessage> _players = new();

        public IReadOnlyList<HexLobbyPlayerMessage> Players => _players;

        public void SelectLocalClass(HexCardProfession profession)
        {
            var payload = JsonUtility.ToJson(new HexProfessionPayload { profession = profession });
#if HEX_MIRROR_PRESENT
            if (isClient)
                CmdSelectClass(payload);
            else
                ApplySelectClass("local-player", payload);
#else
            ApplySelectClass("local-player", payload);
#endif
        }

        public void SetLocalReady(bool ready)
        {
#if HEX_MIRROR_PRESENT
            if (isClient)
                CmdSetReady(ready);
            else
                ApplyReady("local-player", ready);
#else
            ApplyReady("local-player", ready);
#endif
        }

#if HEX_MIRROR_PRESENT
        [Command(requiresAuthority = false)]
        private void CmdSelectClass(string payloadJson, NetworkConnectionToClient sender = null)
        {
            ApplySelectClass(GetPlayerId(sender), payloadJson);
            RpcApplyLobbySnapshot(BuildLobbySnapshotJson());
        }

        [Command(requiresAuthority = false)]
        private void CmdSetReady(bool ready, NetworkConnectionToClient sender = null)
        {
            ApplyReady(GetPlayerId(sender), ready);
            RpcApplyLobbySnapshot(BuildLobbySnapshotJson());
        }

        [ClientRpc]
        private void RpcApplyLobbySnapshot(string snapshotJson)
        {
            ApplyLobbySnapshot(snapshotJson);
        }

        private static string GetPlayerId(NetworkConnectionToClient sender)
        {
            return sender != null ? sender.connectionId.ToString() : "host";
        }
#endif

        private void ApplySelectClass(string playerId, string payloadJson)
        {
            var payload = JsonUtility.FromJson<HexProfessionPayload>(payloadJson);
            var player = GetOrCreatePlayer(playerId);
            player.profession = payload.profession;
            player.hasSelectedProfession = true;
        }

        private void ApplyReady(string playerId, bool ready)
        {
            var player = GetOrCreatePlayer(playerId);
            player.isReady = ready && player.hasSelectedProfession;
        }

        private HexLobbyPlayerMessage GetOrCreatePlayer(string playerId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].playerId == playerId)
                    return _players[i];
            }

            var player = new HexLobbyPlayerMessage
            {
                playerId = playerId,
                displayName = playerId,
                profession = HexCardProfession.Warrior,
            };
            _players.Add(player);
            return player;
        }

        private string BuildLobbySnapshotJson()
        {
            return JsonUtility.ToJson(new HexLobbySnapshot { players = _players });
        }

        private void ApplyLobbySnapshot(string snapshotJson)
        {
            var snapshot = JsonUtility.FromJson<HexLobbySnapshot>(snapshotJson);
            _players.Clear();
            if (snapshot?.players == null)
                return;

            _players.AddRange(snapshot.players);
        }

        [System.Serializable]
        private sealed class HexLobbySnapshot
        {
            public List<HexLobbyPlayerMessage> players = new();
        }
    }
}

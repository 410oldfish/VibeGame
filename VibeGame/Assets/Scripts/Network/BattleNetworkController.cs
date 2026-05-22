using HexDemo;
using UnityEngine;

#if HEX_MIRROR_PRESENT
using Mirror;
#endif

namespace HexDemo.Network
{
#if HEX_MIRROR_PRESENT
    public sealed class BattleNetworkController : NetworkBehaviour
#else
    public sealed class BattleNetworkController : MonoBehaviour
#endif
    {
        private int _nextServerActionId = 1;

        public void SubmitAction(HexActionRequest request)
        {
            string requestJson = JsonUtility.ToJson(request);
#if HEX_MIRROR_PRESENT
            if (isClient && !isServer)
                CmdSubmitAction(requestJson);
            else
                HandleActionOnServer(requestJson);
#else
            HandleActionOnServer(requestJson);
#endif
        }

#if HEX_MIRROR_PRESENT
        [Command(requiresAuthority = false)]
        private void CmdSubmitAction(string requestJson, NetworkConnectionToClient sender = null)
        {
            HandleActionOnServer(requestJson);
        }

        [ClientRpc]
        private void RpcApplyBattleEvents(string eventsJson)
        {
            ApplyBattleEvents(eventsJson);
        }

        [ClientRpc]
        private void RpcApplySnapshot(string snapshotJson)
        {
            ApplySnapshot(snapshotJson);
        }
#endif

        private void HandleActionOnServer(string requestJson)
        {
            var request = JsonUtility.FromJson<HexActionRequest>(requestJson);
            var serverActionId = _nextServerActionId++;

            // The current battle controller still owns resolution. This class is the Mirror message boundary:
            // ActionRequest -> Host Validate/Resolve -> BattleEvents -> Snapshot.
            var eventsMessage = new HexBattleEventsMessage
            {
                serverActionId = serverActionId,
                eventsJson = JsonUtility.ToJson(request),
            };
            var snapshotMessage = new HexBattleSnapshotMessage
            {
                serverActionId = serverActionId,
                snapshotJson = "{}",
            };

            string eventsJson = JsonUtility.ToJson(eventsMessage);
            string snapshotJson = JsonUtility.ToJson(snapshotMessage);
#if HEX_MIRROR_PRESENT
            RpcApplyBattleEvents(eventsJson);
            RpcApplySnapshot(snapshotJson);
#else
            ApplyBattleEvents(eventsJson);
            ApplySnapshot(snapshotJson);
#endif
        }

        private void ApplyBattleEvents(string eventsJson)
        {
            Debug.Log($"BattleEvents {eventsJson}");
        }

        private void ApplySnapshot(string snapshotJson)
        {
            Debug.Log($"BattleSnapshot {snapshotJson}");
        }
    }
}

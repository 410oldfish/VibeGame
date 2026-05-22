using System;
using HexDemo;

namespace HexDemo.Network
{
    public enum HexNetworkRequestType
    {
        LobbyCreateRequest = 0,
        LobbyJoinRequest = 1,
        LobbyLeaveRequest = 2,
        PlayerReadyRequest = 3,
        SelectClassRequest = 4,
        StartGameRequest = 5,
        PlayCardRequest = 6,
        MoveRequest = 7,
        UseSkillRequest = 8,
        UseItemRequest = 9,
        EndTurnRequest = 10,
        ChooseRewardRequest = 11,
        ChooseMapNodeRequest = 12,
    }

    public enum HexBattleEventType
    {
        CardPlayedEvent = 0,
        CardMovedToDiscardEvent = 1,
        CardExhaustedEvent = 2,
        EnergyChangedEvent = 3,
        StaminaChangedEvent = 4,
        HpChangedEvent = 5,
        ArmorChangedEvent = 6,
        DamageEvent = 7,
        HealEvent = 8,
        BuffAddedEvent = 9,
        BuffRemovedEvent = 10,
        BuffStackChangedEvent = 11,
        UnitMovedEvent = 12,
        UnitPushedEvent = 13,
        UnitDiedEvent = 14,
        UnitRevivedEvent = 15,
        CardDrawnEvent = 16,
        CardCreatedEvent = 17,
        TurnStartedEvent = 18,
        TurnEndedEvent = 19,
        EnemyIntentChangedEvent = 20,
        VictoryEvent = 21,
        DefeatEvent = 22,
    }

    [Serializable]
    public sealed class HexLobbyPlayerMessage
    {
        public string playerId;
        public string displayName;
        public HexCardProfession profession;
        public bool hasSelectedProfession;
        public bool isReady;
        public bool isHost;
    }

    [Serializable]
    public sealed class HexActionRequest
    {
        public HexNetworkRequestType type;
        public string playerId;
        public int clientActionId;
        public string payloadJson;
    }

    [Serializable]
    public sealed class HexBattleEventsMessage
    {
        public int serverActionId;
        public string eventsJson;
    }

    [Serializable]
    public sealed class HexBattleSnapshotMessage
    {
        public int serverActionId;
        public string snapshotJson;
    }

    [Serializable]
    public sealed class HexErrorMessage
    {
        public int clientActionId;
        public string errorCode;
        public string message;
    }
}

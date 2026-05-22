using System;
using System.Collections.Generic;

namespace HexDemo
{
    public enum HexNetworkMode
    {
        Offline = 0,
        Host = 1,
        Client = 2,
    }

    public enum HexRoomVisibility
    {
        Public = 0,
        Private = 1,
    }

    public enum HexNetworkCommandType
    {
        SelectProfession = 0,
        ConfirmReady = 1,
        StartRun = 2,
        EnterMapNode = 3,
        MoveUnit = 4,
        PlayCard = 5,
        UseWeaponSkill = 6,
        EndTurn = 7,
    }

    [Serializable]
    public sealed class HexRoomSettings
    {
        public string roomName = "冒险房间";
        public string roomCode = string.Empty;
        public string password = string.Empty;
        public HexRoomVisibility visibility = HexRoomVisibility.Public;
        public int maxPlayers = 3;

        public bool HasPassword => !string.IsNullOrWhiteSpace(password);
    }

    [Serializable]
    public sealed class HexPlayerLobbyState
    {
        public string playerId;
        public string displayName;
        public HexCardProfession profession = HexCardProfession.Warrior;
        public bool hasSelectedProfession;
        public bool isReady;
        public bool isHost;

        public HexPlayerLobbyState Clone()
        {
            return (HexPlayerLobbyState)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class HexNetworkCommand
    {
        public long sequence;
        public string senderPlayerId;
        public HexNetworkCommandType commandType;
        public string payloadJson;
        public double submittedAt;

        public HexNetworkCommand Clone()
        {
            return (HexNetworkCommand)MemberwiseClone();
        }
    }

    public sealed class HexAuthoritativeCommandQueue
    {
        private readonly Queue<HexNetworkCommand> _commands = new();
        private long _nextSequence = 1;

        public int Count => _commands.Count;

        public HexNetworkCommand Accept(string senderPlayerId, HexNetworkCommandType commandType, string payloadJson)
        {
            var command = new HexNetworkCommand
            {
                sequence = _nextSequence++,
                senderPlayerId = senderPlayerId,
                commandType = commandType,
                payloadJson = payloadJson ?? string.Empty,
                submittedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            };

            _commands.Enqueue(command);
            return command.Clone();
        }

        public bool TryDequeue(out HexNetworkCommand command)
        {
            if (_commands.Count == 0)
            {
                command = null;
                return false;
            }

            command = _commands.Dequeue();
            return true;
        }

        public void Clear()
        {
            _commands.Clear();
            _nextSequence = 1;
        }
    }

    [Serializable]
    public sealed class HexProfessionPayload
    {
        public HexCardProfession profession;
    }

    [Serializable]
    public sealed class HexCoordPayload
    {
        public int q;
        public int r;
    }

    [Serializable]
    public sealed class HexCardPlayPayload
    {
        public string runtimeId;
        public string cardId;
        public int targetQ;
        public int targetR;
    }

    [Serializable]
    public sealed class HexWeaponSkillPayload
    {
        public HexWeaponType weaponType;
    }
}

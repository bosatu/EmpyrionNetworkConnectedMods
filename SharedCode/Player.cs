﻿using System;
using System.Collections.Generic;

namespace SharedCode
{
    public class Player : IEquatable<Player>
    {
        public WorldPosition Position { get; private set; }

        public Faction MemberOfFaction { get; private set; }

        public Dictionary<int, float> BpResourcesInFactory { get; private set; }

        public bool IsPrivileged
        {
            get
            {
                // Player = 0, GameMaster = 3, Moderator = 6, Admin = 9 
                return (_permission >= 3) && (_permission <= 9);
            }
        }

        public void AddCredits(double amount)
        {
            _gameServerConnection.SendRequest(Eleon.Modding.CmdId.Request_Player_AddCredits, Eleon.Modding.CmdId.Request_Player_AddCredits, new Eleon.Modding.IdCredits(_entityId, amount));
        }

        public void ChangePlayerfield( WorldPosition newWorldPosition)
        {
            _gameServerConnection.SendRequest(
                Eleon.Modding.CmdId.Request_Player_ChangePlayerfield,
                Eleon.Modding.CmdId.Request_Player_ChangePlayerfield
                , new Eleon.Modding.IdPlayfieldPositionRotation(_entityId, newWorldPosition.playfield.Name, newWorldPosition.position, newWorldPosition.rotation));
        }

        public void SendNormalMessage(string format, params object[] args)
        {
            SendMessage(MessagePriority.Normal, 100, format, args);
        }

        public void SendAlertMessage(string format, params object[] args)
        {
            SendMessage(MessagePriority.Alert, 100, format, args);
        }

        public void SendAttentionMessage(string format, params object[] args)
        {
            SendMessage(MessagePriority.Attention, 100, format, args);
        }

        public void SendMessage(MessagePriority priority, float time, string format, params object[] args)
        {
            string msg = string.Format(format, args);
            _gameServerConnection.SendRequest(
                Eleon.Modding.CmdId.Request_InGameMessage_SinglePlayer,
                Eleon.Modding.CmdId.Request_InGameMessage_SinglePlayer,
                new Eleon.Modding.IdMsgPrio(_entityId, msg, (byte)priority, time));
        }

        #region Common overloads

        public override string ToString()
        {
            return _entityId.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            Player p = obj as Player;
            if (p == null)
            {
                return false;
            }

            return (_entityId == p._entityId);
        }

        public bool Equals(Player other)
        {
            return other != null &&
                   _entityId == other._entityId;
        }

        public override int GetHashCode()
        {
            return -1485059848 + _entityId.GetHashCode();
        }

        public static bool operator ==(Player player1, Player player2)
        {
            return EqualityComparer<Player>.Default.Equals(player1, player2);
        }

        public static bool operator !=(Player player1, Player player2)
        {
            return !(player1 == player2);
        }

        #endregion

        #region Internal Methods

        internal Player(GameServerConnection gameServerConnection, Eleon.Modding.PlayerInfo pInfo)
        {
            _entityId = pInfo.entityId;
            _gameServerConnection = gameServerConnection;
            this.UpdateInfo(pInfo);
        }

        internal void UpdateInfo(Eleon.Modding.PlayerInfo pInfo)
        {
            System.Diagnostics.Debug.Assert(_entityId == pInfo.entityId);
            this.Position = new WorldPosition { playfield = new Playfield(pInfo.playfield), position = new Vector3(pInfo.pos) };
            this.MemberOfFaction = new Faction(pInfo.factionId);
            this.BpResourcesInFactory = pInfo.bpResourcesInFactory;
            _permission = pInfo.permission;
        }

        internal void UpdateInfo(Playfield playfield)
        {
            this.Position = new WorldPosition(playfield, this.Position.position, this.Position.rotation);
        }

        internal int EntityId
        {
            get
            {
                return _entityId;
            }
        }

        #endregion

        #region Private methods

        #endregion

        #region Private Data

        private GameServerConnection _gameServerConnection;
        private int _entityId;
        private int _permission;

        #endregion
    }

    // onPlayerEnteredPlayfield

}
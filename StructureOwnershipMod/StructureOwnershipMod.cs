﻿using SharedCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace StructureOwnershipMod
{
    [System.ComponentModel.Composition.Export(typeof(IGameMod))]
    public class StructureOwnershipMod : IGameMod
    {
        static readonly string k_versionString = SharedCode.Helpers.GetVersionString(typeof(StructureOwnershipMod));

        static readonly string k_saveStateFilePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + "SaveState.yaml";

        public void Start(IGameServerConnection gameServerConnection)
        {
            var configFilePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + "StructureOwnershipMod_Settings.yaml";

            _gameServerConnection = gameServerConnection;
            _config = SharedCode.BaseConfiguration.GetConfiguration<Configuration>(configFilePath);

            _factionRewardTimer = new Timer(_config.CaptureRewardPeriodInMinutes * 1000.0 * 60.0);

            _saveState = SaveState.Load(k_saveStateFilePath);

            _gameServerConnection.AddVersionString(k_versionString);
            _gameServerConnection.Event_Faction_Changed += OnEvent_Faction_Changed;
            _gameServerConnection.Event_ChatMessage += OnEvent_ChatMessage;

            _factionRewardTimer.Elapsed += OnFactionRewardTimer_Elapsed;
            if (!_factionRewardTimer.Enabled)
            {
                _factionRewardTimer.Start();
            }
        }

        public void Stop()
        {
            _saveState.Save(k_saveStateFilePath);
        }

        private void OnEvent_ChatMessage(ChatType chatType, string msg, Player player)
        {
            switch(msg)
            {
                case "/income":
                    {
                        int ownerId = GetOwnerIdForReward(player);

                        if ((ownerId != 0) && _saveState.FactionIdToRewards.ContainsKey(ownerId))
                        {
                            lock (_saveState)
                            {
                                var rewards = _saveState.FactionIdToRewards[ownerId];

                                var task = player.DoItemExchange("test1", "test2", "Process", rewards.ExtractOutForItemExchange());

                                task.ContinueWith(
                                    (Task<Eleon.Modding.ItemExchangeInfo> itemExchangeInfoInTask) =>
                                    {
                                        lock (_saveState)
                                        {
                                            var itemExchangeInfoInQuote = itemExchangeInfoInTask.Result;
                                            rewards.AddStacks(new ItemStacks(itemExchangeInfoInQuote.items));
                                        }
                                    });
                            }

                        }
                        else
                        {
                            player.SendAlarmMessage("No income available.");
                        }
                    }
                    break;
            }
        }

        private void OnEvent_Faction_Changed(Eleon.Modding.FactionChangeInfo obj)
        {
            lock (_saveState)
            {
                if (_config.EntityIdToRewards.ContainsKey(obj.id))
                {
                    if(!_saveState.FactionIdToEntityIds.ContainsKey(obj.factionId))
                    {
                        _saveState.FactionIdToEntityIds.Add(obj.factionId, new HashSet<int>());
                    }

                    _saveState.FactionIdToEntityIds[obj.factionId].Add(obj.id);

                    if (_saveState.EntityIdToFactionId.ContainsKey(obj.id))
                    {
                        // remove entity id from old faction
                        _saveState.FactionIdToEntityIds[_saveState.EntityIdToFactionId[obj.id]].Remove(obj.id);
                    }

                    _saveState.EntityIdToFactionId[obj.id] = obj.factionId;
                }
            }
        }

        private void OnFactionRewardTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var ownersWhoGotSomething = new HashSet<int>();

            lock (_saveState)
            {
                foreach (var factionEntitySet in _saveState.FactionIdToEntityIds)
                {
                    var ownerId = factionEntitySet.Key;

                    var itemStacks = new ItemStacks();

                    foreach (var entityId in factionEntitySet.Value)
                    {
                        itemStacks.AddStacks(_config.EntityIdToRewards[entityId]);
                    }

                    if (itemStacks.Count != 0)
                    {
                        if (!_saveState.FactionIdToRewards.ContainsKey(ownerId))
                        {
                            _saveState.FactionIdToRewards[ownerId] = itemStacks;
                        }
                        else
                        {
                            _saveState.FactionIdToRewards[ownerId].AddStacks(itemStacks);
                        }

                        ownersWhoGotSomething.Add(ownerId);
                    }
                }
            }

            // Tell online players about it
            var onlinePlayersById = _gameServerConnection.GetOnlinePlayers();
            lock (onlinePlayersById)
            {
                lock (_saveState)
                {
                    foreach (var playerId in onlinePlayersById.Keys)
                    {
                        var player = onlinePlayersById[playerId];

                        int ownerId = GetOwnerIdForReward(player);

                        if (ownerId != 0 && ownersWhoGotSomething.Contains(ownerId))
                        {
                            player.SendAttentionMessage("Added income!");
                        }
                    }
                }
            }
        }

        private int GetOwnerIdForReward(Player player)
        {
            int ownerId = 0;

            if (_saveState.FactionIdToEntityIds.ContainsKey(player.MemberOfFaction.Id))
            {
                ownerId = player.MemberOfFaction.Id;
            }
            else if (_saveState.FactionIdToEntityIds.ContainsKey(player.EntityId))
            {
                ownerId = player.EntityId;
            }

            return ownerId;
        }

        private IGameServerConnection _gameServerConnection;
        private Configuration _config;
        private SaveState _saveState;
        private Timer _factionRewardTimer;

    }
}

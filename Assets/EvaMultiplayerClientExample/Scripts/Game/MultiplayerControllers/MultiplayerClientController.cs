using System.Collections.Generic;
using EvaArchitecture.Core;
using EvaArchitecture.Core.Services.MultiplayerServices.Controllers.Bases;
using EvaArchitecture.Core.Services.MultiplayerServices.Helpers;
using EvaArchitecture.Core.Services.MultiplayerServices.Models;
using EvaArchitecture.Core.Services.MultiplayerServices.Services;
using EvaArchitecture.EventHelpers;
using EvaArchitecture.Logger;
using Game.Configs._EvaEvents;
using Game.Controllers.CharacterControllers.PlayerControllers;
using Game.Controllers.LocationControllers;
using UnityEngine;

namespace Game.Controllers.MultiplayerControllers
{
    public class MultiplayerClientController : BaseMultiplayerClientController
    {
        private const int PORT_NUMBER_CREATE_ROOM = 11004;

        // SerializeField used to show in inspector
        [SerializeField] private List<MultiplayerClientOtherController> _multiplayerClientOtherControllers = 
            new List<MultiplayerClientOtherController>();
        
        private bool _isErrorMultiplayer;
        private float _timeErrorMultiplayer;
        private string _errorMultiplayer;

        private GameController GameController => GameController.Instance as GameController;
        private PlayerController PlayerController => GameController.IsNullOrDead() ? null : GameController.PlayerController;

        public override string VersionOfDataFormat => "01";

        public List<MultiplayerClientOtherController> MultiplayerClientOtherControllers => _multiplayerClientOtherControllers;

        public float TimeErrorMultiplayer => _timeErrorMultiplayer;
        public string ErrorMultiplayer => _errorMultiplayer;
        public bool IsErrorMultiplayer => _isErrorMultiplayer;
        public bool IsErrorCreateRoom => IsErrorMultiplayer && !GameController.Instance.IsEnteredMultiplayerLocation;
        public bool IsErrorSendPos => IsErrorMultiplayer && GameController.Instance.IsEnteredMultiplayerLocation;

        protected virtual MultiplayerClientOtherController CreateMultiplayerClientOtherController() =>
            new MultiplayerClientOtherController();
        
        public override bool IsGameOver() => GameController.Instance.IsGameOver;

        public override bool IsOfflineMode() => GameController.Instance.IsOfflineMode;

        public override bool IsEnteredMultiplayerLocation() => GameController.Instance.IsEnteredMultiplayerLocation;

        public override string Nickname() => GameController.Instance.Nickname;
        
        public override int GetPortNumberCreateRoom()
        {
            return PORT_NUMBER_CREATE_ROOM;
        }

        public override bool SetNextPort()
        {
            return false;
        }

        public override string GetRoomFlags()
        {
            return "1000";
        }

        public override string GetRoomCode()
        {
            return "mvr$b"; // means "MagVR $ Battle"
        }

        public override bool OnCreateRoom(string par)
        {
            var multiplayerService = MultiplayerService.Instance;
            if (multiplayerService.IsNullOrDead())
                return (bool) Log.Error(() => $"multiplayerService is null");

            GameController.Instance.IsEnteredMultiplayerLocation = true;
            multiplayerService.IsEnabledSendPos = true;

            UiController.Instance.SetRoomNameText(multiplayerService.Room.nm);
            return true;
        }

        public override void OnErrorCreateRoom(string par1, string par2)
        {
            //_errorMultiplayer = $"{par1} {par2}";
            SetErrorMultiplayer("ERROR: Create room failed, please check internet connection");
            GameController.Instance.IsEnteredMultiplayerLocation = false;
        }
        
        public override void OnErrorSendPos(string par1, string par2)
        {
            //_errorMultiplayer = $"{par1} {par2}";
            SetErrorMultiplayer("ERROR: Lost internet connection");
        }

        private void SetErrorMultiplayer(string error)
        {
            _timeErrorMultiplayer = Time.time;
            _errorMultiplayer = error;
            _isErrorMultiplayer = true;
        }

        public override void OnReceivedMessage(string from, string to, string mes)
        {
        }

        public override void ShowErrorAndRestart(string message)
        {
        }

        public override bool GetDataToSend(MultiplayerDataBuilder dataBuilder)
        {
            var data = new MultiplayerClientData();
            
            if (!data.GetDataToSend(dataBuilder, PlayerController))
                return false;

            return true;
        }

        public override KrvSendMessageInfo GetSendMessageInfo()
        {
            return null;
        }

        public override void ClearSendMessageInfo()
        {
        }

        public override bool DoReceivedData(string receivedData, bool isOfflineMode, out string receivedDataInfo)
        {
            receivedDataInfo = null;
            
            var multiplayerService = MultiplayerService.Instance;
            if (multiplayerService.IsNullOrDead())
                return false;
            
            var result = false;
            List<(PlayerSystemData, string)> receivedStrings = null;
            var mainPlayerIndex = -1;
            try
            {
                // receivedData is null when this method is called each frame between receiving from data
                // In this frames logic also is being called to draw players changing positions (see finally block)
                if (receivedData.IsNullOrEmpty())
                    return false;
                
                _errorMultiplayer = null;
                _isErrorMultiplayer = false;
                Eva.GetEvent<EventMultiplayerReceivedData>().Publish(receivedData);

                receivedStrings = multiplayerService.GetListResultStrings(receivedData, out mainPlayerIndex);
            }
            finally
            {
                result = InternalDoReceivedData(mainPlayerIndex, receivedStrings, isOfflineMode, out receivedDataInfo);
            }
            return result;
        }

        public override void NotifyServerIsNotAvailable(string par1, string par2, MultiplayerCallback callback)
        {
        }
        
        #region private methods

        private bool InternalDoReceivedData(
            int mainPlayerIndex, 
            List<(PlayerSystemData, string)> resultStrings, 
            bool isOfflineMode, 
            out string receivedDataInfo)
        {
            receivedDataInfo = null;
            
            if (IsErrorSendPos)
            {
                var result = DestroyAllChildCharactersForMainPlayer();
                result = result && DestroyAllOtherPlayersAndChildren();
                return result;
            }

            var text = "";
            if (!resultStrings.IsNullOrEmpty())
            {
                for(var i= 0; i < resultStrings.Count; i++)
                {
                    // Insures that controller will exist anyway even if there is no player or data.
                    // This is needed for easy assign child characters from disconnected players to online player  
                    var multiplayerClientOtherController = GetOrCreateMultiplayerClientOtherController(i);
                    if (multiplayerClientOtherController.IsNullOrDead())
                        return (bool) Log.Error(() => $"GetOrCreateMultiplayerClientOtherController FAILED, i={i}");

                    var it = resultStrings[i];
                    if (it.IsNullOrDead())
                        continue;

                    var (playerSystemData, str) = it;
                    if (playerSystemData.IsNullOrDead()
                        || str.IsNullOrEmpty())
                        continue;

                    var multiplayerClientData = Root.CreateMultiplayerClientData();
                    if (!multiplayerClientData.InitWithReceivedResult(str))
                        continue;

                    var mustHaveVersion = VersionOfDataFormat;
                    if (multiplayerClientData.Version != mustHaveVersion)
                        continue;

                    multiplayerClientOtherController.PlayerSystemData = playerSystemData;
                    
                    if (playerSystemData.IsDisconnectedBasedOnServerTime)
                        continue;

                    if (!GetOrCreateOtherPlayer(multiplayerClientOtherController, i, multiplayerClientData))
                    {
                        Log.Error(() =>$"DrawPlayer with index={i} FAILED");
                    }

                    if (!text.IsNullOrEmpty())
                        text += "\n";

                    text += str;
                }
            }
            receivedDataInfo = text.IsNullOrEmpty() ? "EMPTY" : text;
            
            DoAllOtherPlayers(mainPlayerIndex);

            return true;
        }

        private bool DoAllOtherPlayers(int mainPlayerIndex)
        {
            if (_multiplayerClientOtherControllers.IsNullOrEmpty())
                return true; // returns true because there are no other players

            if (mainPlayerIndex >= 0) // protects from initial cases when index < 0
                DestroyOtherPlayersAndTakeChildCharacters(mainPlayerIndex);

            DrawAllOtherPlayers();

            return true;
        }

        private bool DestroyOtherPlayersAndTakeChildCharacters(int mainPlayerIndex)
        {
            var disconnectedItemsUpToConnectedItem = GetDisconnectedItemsUpToConnectedItem(mainPlayerIndex);
            foreach (var multiplayerClientOtherController in _multiplayerClientOtherControllers)
            {
                if (multiplayerClientOtherController.IsNullOrDead()
                    || multiplayerClientOtherController.PlayerSystemData.IsNullOrDead())
                    continue;

                var isOtherPlayerOnline = !multiplayerClientOtherController.PlayerSystemData.IsDisconnectedBasedOnServerTime;
                if (isOtherPlayerOnline)
                    continue;
                
                var contains = !disconnectedItemsUpToConnectedItem.IsNullOrEmpty()
                    && disconnectedItemsUpToConnectedItem.Contains(multiplayerClientOtherController);

                var canTakeChildren = contains;
                if (!multiplayerClientOtherController.TryDeleteDisconnectedPlayer(canTakeChildren))
                    Log.Error(() => $"multiplayerClientOtherController.TryDeleteDisconnectedPlayer FAILED");
            }

            return true;
        }

        private List<MultiplayerClientOtherController> GetDisconnectedItemsUpToConnectedItem(int mainPlayerIndex)
        {
            var count = _multiplayerClientOtherControllers.Count;
            if (count == 0)
                return null; // means nothing to do

            var startIndex = mainPlayerIndex - 1;
            if (startIndex < 0)
                startIndex = count - 1;

            if (!_multiplayerClientOtherControllers.IsIndexValid(startIndex))
            {
                Log.Error(() => $"GetDisconnectedItemsUpToConnectedItem, Wrong startIndex={startIndex}, count={count}");
                return null;
            }
            
            var disconnectedItemsUpToConnectedItem = new List<MultiplayerClientOtherController>();
            var disconnectedItem = _multiplayerClientOtherControllers[startIndex];
            var isDisconnected = IsOtherPlayerDisconnected(disconnectedItem);
            while (isDisconnected)
            {
                if (startIndex == mainPlayerIndex)
                    break;
                
                if (!disconnectedItem.IsNullOrDead()
                    && !disconnectedItem.PlayerSystemData.IsNullOrDead())
                {
                    disconnectedItemsUpToConnectedItem.Add(disconnectedItem);
                }

                startIndex--;
                if (startIndex < 0)
                    startIndex = count - 1;

                disconnectedItem = _multiplayerClientOtherControllers[startIndex];
                isDisconnected = IsOtherPlayerDisconnected(disconnectedItem);
            }
            return disconnectedItemsUpToConnectedItem;
        }

        private bool IsOtherPlayerDisconnected(MultiplayerClientOtherController multiplayerClientOtherController)
        {
            if (multiplayerClientOtherController.IsNullOrDead()
                || multiplayerClientOtherController.PlayerSystemData.IsNullOrDead()
                || multiplayerClientOtherController.PlayerSystemData.IsDisconnectedBasedOnServerTime)
                return true;

            return false;
        }

        private bool DrawAllOtherPlayers()
        {
            if (_multiplayerClientOtherControllers.IsNullOrEmpty())
                return true; // means nothing to do
            
            foreach (var multiplayerClientOtherController in _multiplayerClientOtherControllers)
            {
                if (multiplayerClientOtherController.IsNullOrDead())
                    continue;
                
                var playerSystemData = multiplayerClientOtherController.PlayerSystemData;
                if (playerSystemData.IsNullOrDead())
                    continue;
                
                if (playerSystemData.IsDisconnectedBasedOnServerTime)
                    continue;
                
                if (!multiplayerClientOtherController.Do())
                    continue; // if failed then continues to set other players
            }

            return true;
        }

        private bool GetOrCreateOtherPlayer(
            MultiplayerClientOtherController multiplayerClientOtherController, 
            int indexInReceivedData, 
            MultiplayerClientData multiplayerClientData)
        {
            if (multiplayerClientData.IsNullOrDead())
                return false;

            if (multiplayerClientOtherController.IsNullOrDead())
                return false;
            
            if (!multiplayerClientOtherController.Apply(multiplayerClientData))
                return false;

            if (!InternalGetOrCreateOtherPlayer(multiplayerClientData, indexInReceivedData))
                return false;

            if (!multiplayerClientOtherController.OtherPlayer.IsNullOrDead())
            {
                if (!multiplayerClientOtherController.SetUsingMultiplayerClientData(multiplayerClientData))
                    return false;
            }

            return true;
        }
        
        private bool DestroyAllChildCharactersForMainPlayer()
        {
            if (LocationController.Instance.IsNullOrDead())
                return false;

            var result = LocationController.Instance.DestroyAllChildCharactersForMainPlayer();
            return result;
        }

        private bool DestroyAllOtherPlayersAndChildren()
        {
            if (_multiplayerClientOtherControllers.IsNullOrEmpty())
                return true; // means nothing to destroy

            foreach (var multiplayerClientOtherController in _multiplayerClientOtherControllers)
            {
                if (multiplayerClientOtherController.IsNullOrDead())
                    continue;

                multiplayerClientOtherController.DestroyOtherPlayer();
                multiplayerClientOtherController.DestroyAllChildCharacters();
            }

            return true;
        }

        private MultiplayerClientOtherController GetOrCreateMultiplayerClientOtherController(int indexInReceivedData)
        {
            var needToAddItems = (indexInReceivedData + 1) - _multiplayerClientOtherControllers.Count;
            if (needToAddItems > 0)
            {
                for (var i = 0; i < needToAddItems; i++)
                {
                    _multiplayerClientOtherControllers.Add(CreateMultiplayerClientOtherController());
                }
            }

            if (_multiplayerClientOtherControllers[indexInReceivedData].IsNullOrDead())
                _multiplayerClientOtherControllers[indexInReceivedData] = CreateMultiplayerClientOtherController();
            
            var result = _multiplayerClientOtherControllers[indexInReceivedData];
            return result;
        }

        private bool InternalGetOrCreateOtherPlayer(
            MultiplayerClientData multiplayerClientData,
            int indexInReceivedData)
        {
            if (multiplayerClientData.IsNullOrDead())
                return false;
            
            var characterData = multiplayerClientData.CharacterData;
            if (characterData.IsNullOrDead())
                return false;
            
            if (indexInReceivedData < 0
                || characterData.IsNullOrDead())
                return false;

            if (!_multiplayerClientOtherControllers.IsIndexValid(indexInReceivedData))
                return (bool) Log.Error(() => $"!_multiplayerClientOtherControllers.IsIndexValid(index)");

            var multiplayerClientOtherController = _multiplayerClientOtherControllers[indexInReceivedData];
            if (multiplayerClientOtherController.IsNullOrDead())
                return (bool) Log.Error(() => $"multiplayerClientOtherController is null");
            
            if (!multiplayerClientOtherController.GetOrCreateOtherPlayerCharacter(indexInReceivedData, characterData))
                return (bool) Log.Error(() => $"GetOrCreateOtherCharacter FAILED");

            var childCharactersData = multiplayerClientData.ChildCharactersData;
            if (!multiplayerClientOtherController.GetOrCreateOtherChildCharacters(childCharactersData))
                return (bool) Log.Error(() => $"GetOrCreateOtherChildCharacters FAILED");

            return true;
        }

        #endregion
    }
}

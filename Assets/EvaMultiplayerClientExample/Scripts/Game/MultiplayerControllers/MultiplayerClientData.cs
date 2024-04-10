using System;
using System.Collections.Generic;
using EvaArchitecture.Core.Services.MultiplayerServices.Helpers;
using EvaArchitecture.Core.Services.MultiplayerServices.Models;
using EvaArchitecture.Logger;
using Game.Configs.CharacterConfigs;
using Game.Controllers.CharacterControllers._Bases;
using Game.Controllers.CharacterControllers.EnemyControllers;
using Game.Controllers.CharacterControllers.PlayerControllers;
using Game.Controllers.LocationControllers;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Controllers.MultiplayerControllers
{
    [Serializable]
    public class MultiplayerClientData : BaseMultiplayerClientData
    {
        private const string CHILDS_COUNT_FIELD_NAME = "ecn"; // Enemies CouNt
        private string MainPlayerPrefix => "p";
        private string ChildEnemyPrefix => "e";

        // SerializeField used to show in inspector
        [SerializeField] private MultiplayerCharacterData _characterData = new MultiplayerCharacterData(
            sendPoseName : true,
            sendRotationX: false,
            sendRotationY: true,
            sendRotationZ: false);
        
        // SerializeField used to show in inspector
        [SerializeField] private int _childCount;

        [SerializeField] private List<MultiplayerCharacterData> _childCharactersData = new List<MultiplayerCharacterData>();
        
        public MultiplayerCharacterData CharacterData => _characterData;
        public List<MultiplayerCharacterData> ChildCharactersData => _childCharactersData;

        #region virtual methods
        
        protected override bool InternalGetDataToSend(MultiplayerDataBuilder dataBuilder)
        {
            if (!base.InternalGetDataToSend(dataBuilder))
                return false;

            return true;
        }

        public virtual bool GetDataToSend(
            MultiplayerDataBuilder dataBuilder, 
            PlayerController playerController,
            Func<Animator, string, bool> animatorParsIncludeComparator = null)
        {
            if (!InternalGetDataToSend(dataBuilder))
                return false;
            
            if (dataBuilder.IsNullOrDead())
                return false;
            
            var gameController = GameController.Instance;
            if (gameController.IsNullOrDead())
                return false;

            var playerGameObject = gameController.PlayerGameObject;
            if (playerGameObject.IsNullOrDead())
                return false;

            var trPlayer = playerGameObject.transform;
            if (trPlayer.IsNullOrDead())
                return false;

            var moveBehaviour = playerGameObject.GetComponent<MoveBehaviour>();
            if (moveBehaviour.IsNullOrDead())
                return false;

            var speed = moveBehaviour.Speed;
            
            var characterController = playerGameObject.GetComponent<BaseCharacterController>();
            if (characterController.IsNullOrDead())
                return (bool) Log.Error(() => $"animatorController is null");

            var animatorPars = characterController.GetAnimatorPars(animatorParsIncludeComparator);
            var playerCharacterIndex = gameController.PlayerCharacterIndex;

            if (!_characterData.GetDataToSend(dataBuilder, MainPlayerPrefix, playerCharacterIndex, trPlayer, speed, animatorPars))
                return false;

            if (!GetChildCharactersDataToSend(dataBuilder))
                return false;
            
            return true;
        }

        public override bool InitWithReceivedResult(string receivedData)
        {
            if (!InternalInitWithReceivedResult(receivedData))
                return (bool) Log.Error(() => $"InternalInitWithReceivedResult FAILED");

            return true;
        }
        
        #endregion
        
        #region private methods
        
        private bool GetChildCharactersDataToSend(MultiplayerDataBuilder dataBuilder)
        {
            if (dataBuilder.IsNullOrDead())
                return false;

            var gameController = GameController.Instance;
            if (gameController.IsNullOrDead())
                return false;
            
            var playerGameObject = gameController.PlayerGameObject;
            if (playerGameObject.IsNullOrDead())
                return false;

            var locationController = LocationController.Instance;
            if (locationController.IsNullOrDead())
                return false;
            
            var enemiesParent = locationController.EnemiesParent;
            if (enemiesParent.IsNullOrDead())
                return false;

            if (enemiesParent.childCount == 0)
                return true; // means there is no child characters data

            var index = 0;
            foreach (Transform childTransform in enemiesParent)
            {
                if (childTransform.IsNullOrDead())
                    continue;

                var enemyFsmController = childTransform.GetComponent<EnemyFsmController>();
                if (enemyFsmController.IsNullOrDead())
                    continue;
                
                var navMeshAgent = childTransform.GetComponent<NavMeshAgent>();
                if (navMeshAgent.IsNullOrDead())
                    continue;
                
                if (!enemyFsmController.IsOwnedByMainPlayer)
                    continue;
                
                if (!(enemyFsmController.CharacterConfig is EnemyConfig enemyConfig))
                    continue;
                
                if (!TryGetEnemyCharacterIndexByConfig(enemyConfig, out var characterIndex))
                    continue;

                var multiplayerCharacterData = CreateMultiplayerCharacterData();

                var prefix = GetEnemyPrefix(index);
                var speed = navMeshAgent.speed;

                var animatorPars = enemyFsmController.GetAnimatorCachedPars();
                
                if (!multiplayerCharacterData.GetDataToSend(dataBuilder, prefix, characterIndex, childTransform, speed, animatorPars))
                    return false;

                index++;
            }
            dataBuilder.Add(CHILDS_COUNT_FIELD_NAME, index.ToString());

            return true;
        }

        private bool TryGetEnemyCharacterIndexByConfig(EnemyConfig enemyConfig, out int index)
        {
            index = -1;
            if (enemyConfig.IsNullOrDead())
                return false;
            
            var locationController = LocationController.Instance;
            if (locationController.IsNullOrDead())
                return false;

            var activeAreaConfig = locationController.ActiveAreaConfig;
            if (activeAreaConfig.IsNullOrDead())
                return false;

            var enemyConfigs = activeAreaConfig.EnemyConfigs;
            if (enemyConfigs.IsNullOrEmpty())
                return false;

            for(var i = 0; i < enemyConfigs.Count; i++)
            {
                var it = enemyConfigs[i];
                if (it.IsNullOrDead()
                    || it != enemyConfig)
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        private string GetEnemyPrefix(int index)
        {
            var prefix = $"{ChildEnemyPrefix}{index}";
            return prefix;
        }

        private bool InternalInitWithReceivedResult(string receivedData)
        {
            if (!base.InitWithReceivedResult(receivedData))
                return false;
            
            if (!_characterData.InitWithReceivedResult(_parser, MainPlayerPrefix))
                return false;
            
            _childCharactersData.Clear();
            
            _parser.TryGet(CHILDS_COUNT_FIELD_NAME, out _childCount);
            if (_childCount > 0)
            {
                for (var index = 0; index < _childCount; index++)
                {
                    var multiplayerCharacterData = CreateMultiplayerCharacterData();
                    var prefix = GetEnemyPrefix(index);
                    if (!multiplayerCharacterData.InitWithReceivedResult(_parser, prefix))
                        return (bool) Log.Error(() => $"multiplayerCharacterData.InitWithReceivedResult FAILED");

                    _childCharactersData.Add(multiplayerCharacterData);
                }
            }

            return true;
        }

        private MultiplayerCharacterData CreateMultiplayerCharacterData()
        {
            var multiplayerCharacterData = new MultiplayerCharacterData(
                sendPoseName: true,
                sendRotationX: false,
                sendRotationY: true,
                sendRotationZ: false);
            
            return multiplayerCharacterData;
        }
        
        #endregion
    }
}

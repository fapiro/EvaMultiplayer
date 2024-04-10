using System;
using System.Collections.Generic;
using System.Linq;
using Core.Helpers;
using EvaArchitecture.Core.Services.MultiplayerServices.Controllers.Bases;
using EvaArchitecture.Core.Services.MultiplayerServices.Helpers;
using EvaArchitecture.Core.Services.MultiplayerServices.Models;
using EvaArchitecture.Core.Services.MultiplayerServices.Services;
using EvaArchitecture.Logger;
using Game.Configs.CharacterConfigs;
using Game.Configs.CharacterConfigs._Bases;
using Game.Controllers.CharacterControllers._Bases;
using Game.Controllers.CharacterControllers.EnemyControllers;
using Game.Controllers.CharacterControllers.PlayerControllers;
using Game.Controllers.LocationControllers;
using Game.Helpers;
using UnityEngine;

namespace Game.Controllers.MultiplayerControllers
{
    [Serializable]
    public class MultiplayerClientOtherController : BaseMultiplayerClientOtherController<MultiplayerClientData>
    {
        private PlayerSystemData _playerSystemData;
        
        public PlayerSystemData PlayerSystemData
        {
            get => _playerSystemData;
            set => _playerSystemData = value;
        }

        public virtual bool SetUsingMultiplayerClientData(
            MultiplayerClientData multiplayerClientData)
        {
            return true;
        }
        
        public override bool GetOrCreateOtherPlayerCharacter(
            int siblingIndex,
            MultiplayerCharacterData characterData)
        {
            if (characterData.IsNullOrDead())
                return false;
            
            if (!SetOrCreateOtherPlayerCharacter(siblingIndex, characterData, _otherPlayer))
                return (bool) Log.Error(() => $"SetOrCreateAllChildCharacters FAILED");

            return true;
        }

        public bool TryDeleteDisconnectedPlayer(bool canTakeChildren)
        {
            var multiplayerService = MultiplayerService.Instance;
            if (multiplayerService.IsNullOrDead())
                return false;
            
            if (multiplayerService.OnOtherDisconnectedTakeChildren
                && canTakeChildren
                && CanTakeOtherChildren())
            {
                if (!TakeOtherChildren())
                    return (bool) Log.Error(() => $"TakeOtherChildren FAILED");
            }
            else
            {
                if (!DestroyAllChildCharacters())
                    return (bool) Log.Error(() => $"DestroyAllChildCharacters FAILED");
            }
            
            if (!DestroyOtherPlayer())
                return (bool) Log.Error(() => $"DestroyOtherPlayer FAILED");

            return true;
        }

        private bool CanTakeOtherChildren()
        {
            // means if the main player is online then he can take children of the other player
            var isOnline = MultiplayerSendData.IsOnline;
            if (!isOnline)
                return false;

            if (_childCharacters.IsNullOrEmpty())
                return false; // means nothing to take
            
            if (!PlayerChildrenHelper.TryGetMainPlayerChildren(out var childrenOfMainPlayer))
                return false;
            
            var childrenCount = childrenOfMainPlayer.Count;
            
            var newCount = childrenCount + _childCharacters.Count();
            Log.Info(() => $"newCount = {newCount}");
            var isOkChildrenCount = newCount <= MultiplayerService.PLAYER_MAX_CHILDREN_COUNT;
            if (!isOkChildrenCount)
                return false;
            
            return true;
        }
        
        private bool TakeOtherChildren()
        {
            var locationController = LocationController.Instance;
            if (locationController.IsNullOrDead())
                return false;
            
            if (_childCharacters.IsNullOrEmpty())
                return true; // means nothing to take

            var taken = new List<GameObject>();
            foreach (var child in _childCharacters)
            {
                if (child.IsNullOrDead())
                    continue;
                
                taken.Add(child);
            }
            _childCharacters.Clear();
            
            if (taken.IsNullOrEmpty())
                return true; // means nothing to take

            foreach (var go in taken)
            {
                if (go.IsNullOrDead())
                    continue;

                if (!locationController.ChangeToEnemyOfMainPlayer(go))
                {
                    Log.Error(() => $"locationController.ChangeToEnemyOfMainPlayer FAILED, Destroying gameObject={go.GetPathInScene()}");
                    UnityEngine.Object.Destroy(go); // means if failed to take then destroys
                }
            }
            
            return true;
        }

        public override GameObject SetOrCreateOtherPlayerCharacter(
            int siblingIndex,
            MultiplayerCharacterData characterData,
            GameObject otherCharacter)
        {
            var gameConfig = Root.GameConfig;
            if (gameConfig.IsNullOrDead())
                return null;

            if (characterData.IsNullOrDead())
                return null;

            var characterIndex = characterData.CharacterIndex;

            var playerConfigs = gameConfig.PlayerConfigs;
            if (playerConfigs.IsNullOrEmpty()
                || !playerConfigs.IsIndexValid(characterIndex))
            {
                Log.Error(() => $"!playerConfigs.IsIndexValid(characterIndex), characterIndex={characterIndex}");
                return null;
            }

            var playerConfig = playerConfigs[characterIndex];
            if (playerConfig.IsNullOrDead())
            {
                Log.Error(() => $"playerConfig.IsNullOrDead()");
                return null;
            }

            otherCharacter = GetOrCreateOtherCharacter(siblingIndex, characterData, playerConfig, otherCharacter);
            
            _otherPlayer = otherCharacter;
            return _otherPlayer;
        }

        public override GameObject GetOrCreateOtherCharacter(
            int siblingIndex,
            MultiplayerCharacterData characterData,
            object info,
            GameObject otherCharacter)
        {
            if (info.IsNullOrDead())
                return null;
            
            var locationController = LocationController.Instance;
            if (locationController.IsNullOrDead())
                return null;

            if (!(info is BaseCharacterConfig characterConfig))
                return null;

            if (!otherCharacter.IsNullOrDead())
            {
                if (!TryGetIsSameCharacter(otherCharacter, characterConfig, out var isSame))
                    return null;

                if (isSame) // if character is not changed
                    return otherCharacter;
                
                UnityEngine.Object.Destroy(otherCharacter); // not same, Destroys the gameObject
                otherCharacter = null;
            }

            BaseCharacterController characterController = null;
            if (info is PlayerConfig playerConfig)
            {
                otherCharacter = CreateOtherPlayerCharacter(characterData, siblingIndex, playerConfig);
                characterController = otherCharacter.GetOrAddComponent<PlayerController>();
            }
            else if (info is EnemyConfig enemyConfig)
            {
                otherCharacter = locationController.CreateChildCharacter(enemyConfig, characterData);
                characterController = otherCharacter.GetOrAddComponent<MultiplayerOtherChildCharacterController>();
            }

            if (otherCharacter.IsNullOrDead())
            {
                Log.Error(() => $"otherCharacter is null");
                return null;
            }
            
            if (characterController.IsNullOrDead())
            {
                Log.Error(() => $"characterController is null");
                return null;
            }
            
            var animator = otherCharacter.GetComponent<Animator>();
            if (animator.IsNullOrDead())
                Log.Error(() => $"animator is null");
            
            if (!characterController.Init(characterConfig, animator))
                Log.Error(() => $"characterController.Init FAILED");

            return otherCharacter;
        }

        private bool TryGetIsSameCharacter(GameObject characterGameObject, BaseCharacterConfig neededCharacterConfig, out bool isSame)
        {
            isSame = false;
            if (characterGameObject.IsNullOrDead()) 
                return false;
            
            var characterController = characterGameObject.GetComponent<BaseCharacterController>();
            if (characterController.IsNullOrDead())
                return (bool) Log.Error(() => $"playerController is null");

            var usedConfig = characterController.CharacterConfig;
            isSame = usedConfig == neededCharacterConfig; // Compares configs
            return true;
        }

        public bool Do()
        {
            var result = DoOtherPlayer(_otherPlayer, MultiplayerClientData.CharacterData);
            if (!result)
                Log.Error(() => $"DoOtherPlayer FAILED"); // here continues execution to process childCharacters

            if (!_multiplayerClientData.IsNullOrDead())
            {
                var childCharactersData = _multiplayerClientData.ChildCharactersData;
                for (var i = 0; i < childCharactersData.Count; i++)
                {
                    var childData = childCharactersData[i];
                    if (childData.IsNullOrDead())
                        continue;

                    if (i >= _childCharacters.Count)
                        break;

                    var childCharacter = _childCharacters[i];
                    result = result && DoOtherPlayer(childCharacter, childData);
                }
            }

            return result;
        }

        public bool DoOtherPlayer(GameObject otherPlayer, MultiplayerCharacterData characterData)
        {
            if (otherPlayer.IsNullOrDead())
                return true; // means other player does not have character right now
            
            var rb = otherPlayer.GetComponent<Rigidbody>();
            if (!rb.IsNullOrDead())
            {
                rb.useGravity = false; // fixes problem for other player when he can not go up to the hill (we move him using MoveTowards) but RigidBody.useGravity moves him down
            }

            var characterController = otherPlayer.GetComponent<BaseCharacterController>();
            if (characterController.IsNullOrDead())
                return (bool) Log.Error(() => $"characterController is null");

            if (!characterData.IsNullOrDead())
            {
                var animatorPars = characterData.AnimatorPars;
                if (!animatorPars.IsNullOrEmpty())
                {
                    AnimatorSetPose(characterController, animatorPars);
                }
                
                if (!characterController.SetAnimatorParameters(animatorPars))
                    return false;

                if (!characterData.SetPositionAndRotation(characterController))
                    return false;
            }

            return true;
        }

        protected override object GetCreationInfoOtherChildCharacter(MultiplayerCharacterData childCharacterData)
        {
            if (childCharacterData.IsNullOrDead())
                return null;
            
            var locationController = LocationController.Instance;
            if (locationController.IsNullOrDead())
                return null;

            var activeAreaConfig = locationController.ActiveAreaConfig;
            if (activeAreaConfig.IsNullOrDead())
                return null;

            var enemyConfigs = activeAreaConfig.EnemyConfigs;
            if (enemyConfigs.IsNullOrEmpty())
                return null;
            
            var characterIndex = childCharacterData.CharacterIndex;
            if (!enemyConfigs.IsIndexValid(characterIndex))
                return null;

            var enemyConfig = enemyConfigs[characterIndex];
            if (enemyConfig.IsNullOrDead())
                return null;
            
            return enemyConfig;
        }

        private GameObject CreateOtherPlayerCharacter(
            MultiplayerCharacterData characterData,
            int siblingIndex,
            BaseCharacterConfig config)
        {
            if (characterData.IsNullOrDead())
                return null;
            
            var gameController = GameController.Instance;
            if (gameController.IsNullOrDead())
                return null;
                
            var playersParent = gameController.PlayersParent;
            if (playersParent.IsNullOrDead())
            {
                Log.Error(() =>$"gameController.PlayersParent is empty");
                return null;
            }

            var prefab = config.Prefab;
            if (prefab.IsNullOrDead())
            {
                Log.Error(() =>$"prefab is null");
                return null;
            }
            
            var otherPlayer = UnityEngine.Object.Instantiate(prefab, playersParent);
            otherPlayer.name = config.name;
            otherPlayer.transform.position = characterData.Position;
            otherPlayer.transform.rotation = characterData.Rotation;

            var behaviours = otherPlayer.GetComponents<GenericBehaviour>();
            if (!behaviours.IsNullOrDead())
            {
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.IsNullOrDead())
                        continue;
                    
                    UnityEngine.Object.Destroy(behaviour);
                }
            }

            var basicBehaviour = otherPlayer.GetComponent<BasicBehaviour>();
            if (!basicBehaviour.IsNullOrDead())
                UnityEngine.Object.Destroy(basicBehaviour);

            var animator = otherPlayer.GetComponent<Animator>();
            if (!animator.IsNullOrDead())
                animator.applyRootMotion = false;

            if (!config.SetNavMeshObstacle(otherPlayer))
                Log.Error(() => $"SetNavMeshObstacle FAILED");

            otherPlayer.transform.SetSiblingIndex(siblingIndex);
                
            if (!otherPlayer.activeSelf)
                otherPlayer.SetActive(true);

            return otherPlayer;
        }
        
        public override bool GetOrCreateOtherChildCharacters(List<MultiplayerCharacterData> childCharactersData)
        {
            if (childCharactersData.IsNullOrEmpty())
            {
                if (!DestroyAllChildCharacters())
                    return (bool) Log.Error(() => $"DestroyAllChildCharacters FAILED");
            }
            else
            {
                if (!SetOrCreateAllChildCharacters(childCharactersData))
                    return (bool) Log.Error(() => $"SetOrCreateAllChildCharacters FAILED");
            }

            return true;
        }
        
        public static bool AnimatorSetPose(
            BaseCharacterController characterController,
            string animatorPars)
        {
            if (characterController.IsNullOrDead()
                || animatorPars.IsNullOrEmpty()) 
                return false;

            var otherPlayerController = characterController as PlayerController;
            if (!otherPlayerController.IsNullOrDead())
            {
                if (!otherPlayerController.SetPose(animatorPars))
                    return false;
            }
            
            return true;
        }
    }
}

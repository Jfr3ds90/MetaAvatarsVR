using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MetaAvatarsVR.Networking.PuzzleSync
{
    public enum InteractableState
    {
        Idle,
        Hovering,
        Selected,
        Activated,
        Disabled
    }
    
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedInteractable : NetworkBehaviour
    {
        [Header("Interactable Configuration")]
        [SerializeField] protected XRBaseInteractable _xrInteractable;
        [SerializeField] protected bool _requireOwnership = false;
        [SerializeField] protected bool _allowMultipleUsers = false;
        [SerializeField] protected float _cooldownTime = 0.5f;
        
        [Header("Visual Feedback")]
        [SerializeField] protected GameObject _hoverVisual;
        [SerializeField] protected GameObject _selectedVisual;
        [SerializeField] protected GameObject _activatedVisual;
        [SerializeField] protected AudioClip _interactSound;
        [SerializeField] protected AudioClip _releaseSound;
        
        [Header("Network State")]
        [Networked] public InteractableState CurrentState { get; set; }
        [Networked] public NetworkBool IsLocked { get; set; }
        [Networked] public PlayerRef CurrentUser { get; set; }
        [Networked] public float LastInteractionTime { get; set; }
        [Networked] public int InteractionCount { get; set; }
        
        [Header("Events")]
        public UnityEvent<PlayerRef> OnNetworkHoverEnter = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnNetworkHoverExit = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnNetworkSelectEnter = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnNetworkSelectExit = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnNetworkActivate = new UnityEvent<PlayerRef>();
        public UnityEvent<PlayerRef> OnNetworkDeactivate = new UnityEvent<PlayerRef>();
        public UnityEvent<InteractableState> OnStateChanged = new UnityEvent<InteractableState>();
        
        protected AudioSource _audioSource;
        protected InteractableState _previousState;
        protected bool _isLocallyInteracting = false;
        
        protected virtual void Awake()
        {
            if (_xrInteractable == null)
                _xrInteractable = GetComponent<XRBaseInteractable>();
                
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
                
            SetupXRInteractableEvents();
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                CurrentState = InteractableState.Idle;
                IsLocked = false;
                CurrentUser = PlayerRef.None;
                LastInteractionTime = 0f;
                InteractionCount = 0;
            }
            
            UpdateVisualState(CurrentState);
        }
        
        protected virtual void SetupXRInteractableEvents()
        {
            if (_xrInteractable == null)
                return;
                
            _xrInteractable.hoverEntered.AddListener(OnLocalHoverEnter);
            _xrInteractable.hoverExited.AddListener(OnLocalHoverExit);
            _xrInteractable.selectEntered.AddListener(OnLocalSelectEnter);
            _xrInteractable.selectExited.AddListener(OnLocalSelectExit);
            
            if (_xrInteractable is XRSimpleInteractable simpleInteractable)
            {
                simpleInteractable.activated.AddListener(OnLocalActivate);
                simpleInteractable.deactivated.AddListener(OnLocalDeactivate);
            }
        }
        
        protected virtual void OnLocalHoverEnter(HoverEnterEventArgs args)
        {
            if (!CanInteract())
                return;
                
            RPC_RequestStateChange(InteractableState.Hovering, Runner.LocalPlayer);
        }
        
        protected virtual void OnLocalHoverExit(HoverExitEventArgs args)
        {
            if (_isLocallyInteracting)
                return;
                
            RPC_RequestStateChange(InteractableState.Idle, Runner.LocalPlayer);
        }
        
        protected virtual void OnLocalSelectEnter(SelectEnterEventArgs args)
        {
            if (!CanInteract())
                return;
                
            _isLocallyInteracting = true;
            RPC_RequestStateChange(InteractableState.Selected, Runner.LocalPlayer);
        }
        
        protected virtual void OnLocalSelectExit(SelectExitEventArgs args)
        {
            _isLocallyInteracting = false;
            RPC_RequestStateChange(InteractableState.Idle, Runner.LocalPlayer);
        }
        
        protected virtual void OnLocalActivate(ActivateEventArgs args)
        {
            if (!CanInteract())
                return;
                
            RPC_RequestActivation(Runner.LocalPlayer);
        }
        
        protected virtual void OnLocalDeactivate(DeactivateEventArgs args)
        {
            RPC_RequestDeactivation(Runner.LocalPlayer);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestStateChange(InteractableState newState, PlayerRef player, RpcInfo info = default)
        {
            if (!ValidateStateChange(newState, player))
                return;
                
            CurrentState = newState;
            CurrentUser = player;
            LastInteractionTime = Runner.SimulationTime;
            
            RPC_NotifyStateChange(newState, player);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestActivation(PlayerRef player, RpcInfo info = default)
        {
            if (!CanActivate(player))
                return;
                
            CurrentState = InteractableState.Activated;
            CurrentUser = player;
            LastInteractionTime = Runner.SimulationTime;
            InteractionCount++;
            
            PerformActivation(player);
            
            RPC_NotifyActivation(player);
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        protected virtual void RPC_RequestDeactivation(PlayerRef player, RpcInfo info = default)
        {
            if (CurrentUser != player && CurrentUser != PlayerRef.None)
                return;
                
            CurrentState = InteractableState.Idle;
            CurrentUser = PlayerRef.None;
            
            PerformDeactivation(player);
            
            RPC_NotifyDeactivation(player);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyStateChange(InteractableState newState, PlayerRef player)
        {
            UpdateVisualState(newState);
            OnStateChanged?.Invoke(newState);
            
            switch (newState)
            {
                case InteractableState.Hovering:
                    OnNetworkHoverEnter?.Invoke(player);
                    break;
                case InteractableState.Selected:
                    OnNetworkSelectEnter?.Invoke(player);
                    PlaySound(_interactSound);
                    break;
                case InteractableState.Idle:
                    if (_previousState == InteractableState.Hovering)
                        OnNetworkHoverExit?.Invoke(player);
                    else if (_previousState == InteractableState.Selected)
                    {
                        OnNetworkSelectExit?.Invoke(player);
                        PlaySound(_releaseSound);
                    }
                    break;
            }
            
            _previousState = newState;
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyActivation(PlayerRef player)
        {
            OnNetworkActivate?.Invoke(player);
            UpdateVisualState(InteractableState.Activated);
            PlaySound(_interactSound);
            
            Debug.Log($"[NetworkedInteractable] {gameObject.name} activated by player {player}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyDeactivation(PlayerRef player)
        {
            OnNetworkDeactivate?.Invoke(player);
            UpdateVisualState(InteractableState.Idle);
            PlaySound(_releaseSound);
            
            Debug.Log($"[NetworkedInteractable] {gameObject.name} deactivated by player {player}");
        }
        
        protected virtual void PerformActivation(PlayerRef player)
        {
            // Override in derived classes for specific activation logic
        }
        
        protected virtual void PerformDeactivation(PlayerRef player)
        {
            // Override in derived classes for specific deactivation logic
        }
        
        protected virtual bool ValidateStateChange(InteractableState newState, PlayerRef player)
        {
            if (IsLocked)
                return false;
                
            if (!_allowMultipleUsers && CurrentUser != PlayerRef.None && CurrentUser != player)
                return false;
                
            if (Runner.SimulationTime - LastInteractionTime < _cooldownTime)
                return false;
                
            return true;
        }
        
        protected virtual bool CanInteract()
        {
            if (IsLocked)
                return false;
                
            if (CurrentState == InteractableState.Disabled)
                return false;
                
            if (!_allowMultipleUsers && CurrentUser != PlayerRef.None && CurrentUser != Runner.LocalPlayer)
                return false;
                
            return true;
        }
        
        protected virtual bool CanActivate(PlayerRef player)
        {
            return CanInteract() && (CurrentUser == player || CurrentUser == PlayerRef.None);
        }
        
        protected virtual void UpdateVisualState(InteractableState state)
        {
            if (_hoverVisual != null)
                _hoverVisual.SetActive(state == InteractableState.Hovering);
                
            if (_selectedVisual != null)
                _selectedVisual.SetActive(state == InteractableState.Selected);
                
            if (_activatedVisual != null)
                _activatedVisual.SetActive(state == InteractableState.Activated);
        }
        
        protected virtual void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetLocked(NetworkBool locked, RpcInfo info = default)
        {
            if (HasStateAuthority)
            {
                IsLocked = locked;
                
                if (locked)
                {
                    CurrentState = InteractableState.Disabled;
                    CurrentUser = PlayerRef.None;
                }
                else
                {
                    CurrentState = InteractableState.Idle;
                }
                
                RPC_NotifyLockStateChanged(locked);
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        protected virtual void RPC_NotifyLockStateChanged(NetworkBool locked)
        {
            if (_xrInteractable != null)
            {
                _xrInteractable.enabled = !locked;
            }
            
            UpdateVisualState(locked ? InteractableState.Disabled : InteractableState.Idle);
            
            Debug.Log($"[NetworkedInteractable] {gameObject.name} locked state: {locked}");
        }
        
        public void ResetInteractable()
        {
            if (HasStateAuthority)
            {
                CurrentState = InteractableState.Idle;
                CurrentUser = PlayerRef.None;
                IsLocked = false;
                InteractionCount = 0;
                LastInteractionTime = 0f;
            }
        }
        
        public bool IsBeingUsed()
        {
            return CurrentUser != PlayerRef.None;
        }
        
        public bool IsOwnedByLocalPlayer()
        {
            return CurrentUser == Runner.LocalPlayer;
        }
        
        protected virtual void OnDestroy()
        {
            if (_xrInteractable != null)
            {
                _xrInteractable.hoverEntered.RemoveListener(OnLocalHoverEnter);
                _xrInteractable.hoverExited.RemoveListener(OnLocalHoverExit);
                _xrInteractable.selectEntered.RemoveListener(OnLocalSelectEnter);
                _xrInteractable.selectExited.RemoveListener(OnLocalSelectExit);
                
                if (_xrInteractable is XRSimpleInteractable simpleInteractable)
                {
                    simpleInteractable.activated.RemoveListener(OnLocalActivate);
                    simpleInteractable.deactivated.RemoveListener(OnLocalDeactivate);
                }
            }
        }
    }
}
using System;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedMusicalNote : NetworkBehaviour
    {
        [Header("Note Configuration")]
        [SerializeField] private int _noteIndex = 0; 
        [SerializeField] private string _noteName = "Do";
        [SerializeField] private AudioClip _noteSound;
        
        [Header("Visual")]
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private GameObject _glowEffect;
        
        [Header("Network State")]
        [Networked] public int NoteIndex { get; set; }
        [Networked] public int ColorIndex { get; set; }
        [Networked] public NetworkBool IsPlaced { get; set; }
        [Networked] public int CurrentSlotIndex { get; set; }
        
        [Header("Events")]
        public UnityEvent<int> OnNotePlaced = new UnityEvent<int>();
        public UnityEvent OnNoteRemoved = new UnityEvent();
        
        private Grabbable _grabbable;
        private Rigidbody _rigidbody;
        private AudioSource _audioSource;
        private NetworkedMusicalNotesPuzzle _puzzleController;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        
        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                _grabbable = gameObject.AddComponent<Grabbable>();
            }
            
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
                
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }
        
        private void Start()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnGrabbableEvent;
            }
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                NoteIndex = _noteIndex;
                CurrentSlotIndex = -1;
                IsPlaced = false;
            }
        }
        
        private void OnGrabbableEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                OnGrabbed();
            }
            else if (evt.Type == PointerEventType.Unselect)
            {
                OnReleased();
            }
        }
        
        private void OnGrabbed()
        {
            if (IsPlaced)
            {
                RPC_RemoveFromSlot();
            }
        }
        
        private void OnReleased()
        {
            CheckNearbySlot();
        }
        
        private void CheckNearbySlot()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 0.3f);
            
            foreach (var collider in colliders)
            {
                var slot = collider.GetComponent<NetworkedNoteSlot>();
                if (slot != null && !slot.IsOccupied)
                {
                    RPC_RequestPlacement(slot.SlotIndex);
                    return;
                }
            }
            
            StartCoroutine(ReturnToOriginAfterDelay());
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPlacement(int slotIndex, RpcInfo info = default)
        {
            if (_puzzleController != null)
            {
                bool success = _puzzleController.TryPlaceNoteInSlot(this, slotIndex);
                
                if (success)
                {
                    IsPlaced = true;
                    CurrentSlotIndex = slotIndex;
                    RPC_PlacementSuccess(slotIndex);
                }
                else
                {
                    RPC_PlacementFailed();
                }
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlacementSuccess(int slotIndex)
        {
            OnNotePlaced?.Invoke(slotIndex);
            PlaySound();
            
            if (_glowEffect != null)
                _glowEffect.SetActive(true);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlacementFailed()
        {
            ReturnToOrigin();
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RemoveFromSlot(RpcInfo info = default)
        {
            if (_puzzleController != null && CurrentSlotIndex >= 0)
            {
                _puzzleController.RemoveNoteFromSlot(CurrentSlotIndex);
                IsPlaced = false;
                CurrentSlotIndex = -1;
            }
        }
        
        public void SetPuzzleController(NetworkedMusicalNotesPuzzle controller)
        {
            _puzzleController = controller;
        }
        
        public void SetColor(Material colorMaterial)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.material = colorMaterial;
            }
        }
        
        public void SetNoteIndex(int index)
        {
            _noteIndex = index;
            if (HasStateAuthority)
            {
                NoteIndex = index;
            }
        }
        
        public int GetNoteIndex()
        {
            return _noteIndex;
        }
        
        public void EnableInteraction(bool enabled)
        {
            var grabbable = GetComponent<Grabbable>();
            if (grabbable != null)
            {
                // Aquí puedes habilitar/deshabilitar la interacción si es necesario
            }
        }
        
        public void ReturnToOrigin()
        {
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        
        private System.Collections.IEnumerator ReturnToOriginAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            if (!IsPlaced)
            {
                ReturnToOrigin();
            }
        }
        
        private void PlaySound()
        {
            if (_noteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_noteSound);
            }
        }
        
        public void ResetNote()
        {
            IsPlaced = false;
            CurrentSlotIndex = -1;
            ReturnToOrigin();
            
            if (_glowEffect != null)
                _glowEffect.SetActive(false);
        }
        
        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised -= OnGrabbableEvent;
            }
        }
    }
}
using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedVRLeverFixed : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        
        [Header("Angular Settings")]
        [SerializeField] private float _restAngle = -120f;
        [SerializeField] private float _activationAngle = -55f;
        [SerializeField] private float _minAngle = -130f;
        [SerializeField] private float _maxAngle = -45f;
        
        [Header("References")]
        [SerializeField] private Transform _leverPivot;
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Events")]
        public UnityEvent<string> OnLeverActivated;
        public UnityEvent<string> OnLeverDeactivated;
        
        [Networked] public float NetworkedAngle { get; set; }
        [Networked] public NetworkBool IsActivated { get; set; }
        [Networked] public NetworkBool IsGrabbed { get; set; }
        
        private Grabbable _grabbable;
        private HandGrabInteractable _handGrabInteractable;
        private Rigidbody _rigidbody;
        
        private bool _isBeingGrabbed = false;
        private Vector3 _grabStartLocalPoint;
        private float _grabStartAngle;
        private Transform _grabbingHand;
        private bool _wasActivated = false;
        
        private void Awake()
        {
            SetupComponents();
            SetInitialRotation();
        }
        
        private void SetupComponents()
        {
            if (_leverPivot == null)
                _leverPivot = transform;
            
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezePosition | 
                                    RigidbodyConstraints.FreezeRotationX | 
                                    RigidbodyConstraints.FreezeRotationY;
            
            _grabbable = GetComponent<Grabbable>();
            if (_grabbable == null)
            {
                _grabbable = gameObject.AddComponent<Grabbable>();
            }
            
           
            var freeTransformer = gameObject.AddComponent<GrabFreeTransformer>();
            _grabbable.InjectOptionalOneGrabTransformer(freeTransformer);
            
            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            if (_handGrabInteractable == null)
            {
                _handGrabInteractable = gameObject.AddComponent<HandGrabInteractable>();
                _handGrabInteractable.InjectOptionalPointableElement(_grabbable);
                _handGrabInteractable.InjectRigidbody(_rigidbody);
            }
            
            _grabbable.WhenPointerEventRaised += OnPointerEvent;
        }
        
        private void SetInitialRotation()
        {
            _leverPivot.localRotation = Quaternion.Euler(0, 0, _restAngle);
        }
        
        private void OnPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    StartGrab(evt);
                    break;
                    
                case PointerEventType.Move:
                    UpdateGrab(evt);
                    break;
                    
                case PointerEventType.Unselect:
                    EndGrab(evt);
                    break;
            }
        }
        
        private void StartGrab(PointerEvent evt)
        {
            _isBeingGrabbed = true;
            _grabStartAngle = GetCurrentAngle();
            
            if (evt.Pose.position != Vector3.zero)
            {
                _grabStartLocalPoint = _leverPivot.InverseTransformPoint(evt.Pose.position);
            }
            
            _rigidbody.isKinematic = false;
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(true);
            }
            
            Debug.Log($"[Lever] Grab started at angle {_grabStartAngle:F1}°");
        }
        
        private void UpdateGrab(PointerEvent evt)
        {
            if (!_isBeingGrabbed || evt.Pose.position == Vector3.zero) return;
            
            Vector3 currentLocalPoint = _leverPivot.InverseTransformPoint(evt.Pose.position);
            
            float angleFromStart = Mathf.Atan2(_grabStartLocalPoint.x, _grabStartLocalPoint.y) * Mathf.Rad2Deg;
            float angleFromCurrent = Mathf.Atan2(currentLocalPoint.x, currentLocalPoint.y) * Mathf.Rad2Deg;
            
            float deltaAngle = angleFromCurrent - angleFromStart;
            float targetAngle = _grabStartAngle + deltaAngle;
            
            targetAngle = Mathf.Clamp(targetAngle, _minAngle, _maxAngle);
            
            ApplyRotation(targetAngle);
        }
        
        private void EndGrab(PointerEvent evt)
        {
            _isBeingGrabbed = false;
            _rigidbody.isKinematic = true;
            
            float finalAngle = GetCurrentAngle();
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(false);
                RPC_UpdateAngle(finalAngle);
            }
            
            Debug.Log($"[Lever] Grab ended at angle {finalAngle:F1}°");
        }
        
        private void ApplyRotation(float angle)
        {
            Vector3 currentEuler = _leverPivot.localEulerAngles;
            _leverPivot.localRotation = Quaternion.Euler(0, 0, angle);
        }
        
        private float GetCurrentAngle()
        {
            float angle = _leverPivot.localEulerAngles.z;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                NetworkedAngle = _restAngle;
                IsActivated = false;
                IsGrabbed = false;
            }
            
            ApplyRotation(NetworkedAngle);
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            if (IsGrabbed && _isBeingGrabbed)
            {
                NetworkedAngle = GetCurrentAngle();
            }
            
            bool shouldBeActive = NetworkedAngle >= _activationAngle;
            if (shouldBeActive != _wasActivated)
            {
                IsActivated = shouldBeActive;
                _wasActivated = shouldBeActive;
                RPC_NotifyActivation(IsActivated);
            }
        }
        
        public override void Render()
        {
            if (!_isBeingGrabbed)
            {
                float current = GetCurrentAngle();
                float target = NetworkedAngle;
                ApplyRotation(Mathf.LerpAngle(current, target, Time.deltaTime * 10f));
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetGrabbed(NetworkBool grabbed)
        {
            IsGrabbed = grabbed;
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_UpdateAngle(float angle)
        {
            NetworkedAngle = angle;
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyActivation(NetworkBool activated)
        {
            if (activated)
                OnLeverActivated?.Invoke(_leverLetter);
            else
                OnLeverDeactivated?.Invoke(_leverLetter);
        }
    }
}
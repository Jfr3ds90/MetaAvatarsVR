using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Grabbable))]
    [RequireComponent(typeof(OneGrabRotateTransformer))]
    public class NetworkedLever : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        
        [Header("Positive Angles Configuration")]
        [SerializeField] private float _restAngle = 120f;      // Ahora positivo
        [SerializeField] private float _activationAngle = 55f; // Ahora positivo
        [SerializeField] private float _minAngle = 45f;        // Límite superior (palanca arriba)
        [SerializeField] private float _maxAngle = 130f;       // Límite inferior (palanca abajo)
        
        [Header("References")]
        [SerializeField] private Transform _leverPivot;
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Events")]
        public UnityEvent<string> OnLeverActivated;
        public UnityEvent<string> OnLeverDeactivated;
        
        // Network State
        [Networked] public float NetworkedAngle { get; set; }
        [Networked] public NetworkBool IsActivated { get; set; }
        [Networked] public NetworkBool IsGrabbed { get; set; }
        
        // Components
        private Grabbable _grabbable;
        private OneGrabRotateTransformer _rotateTransformer;
        private HandGrabInteractable _handGrabInteractable;
        
        // State
        private bool _isLocallyGrabbing = false;
        private bool _wasActivated = false;
        
        private void Awake()
        {
            if (_leverPivot == null)
                _leverPivot = transform;
            
            SetupComponents();
            SetInitialRotation();
        }
        
        private void SetupComponents()
        {
            // Get Grabbable
            _grabbable = GetComponent<Grabbable>();
            
            // Get OneGrabRotateTransformer and configure it
            _rotateTransformer = GetComponent<OneGrabRotateTransformer>();
            if (_rotateTransformer != null)
            {
                // Configure for Z axis rotation with positive angles
               // _rotateTransformer.Rotation = OneGrabRotateTransformer.Axis.Z;
                //_rotateTransformer.Pivot = _leverPivot;
                
                // Set constraints with POSITIVE angles
                _rotateTransformer.Constraints = new OneGrabRotateTransformer.OneGrabRotateConstraints
                {
                    MinAngle = new FloatConstraint 
                    { 
                        Constrain = true, 
                        Value = _minAngle  // 45° (palanca arriba)
                    },
                    MaxAngle = new FloatConstraint 
                    { 
                        Constrain = true, 
                        Value = _maxAngle  // 130° (palanca abajo)
                    }
                };
            }
            
            // Setup HandGrabInteractable
            _handGrabInteractable = GetComponent<HandGrabInteractable>();
            if (_handGrabInteractable == null)
            {
                _handGrabInteractable = gameObject.AddComponent<HandGrabInteractable>();
                _handGrabInteractable.InjectOptionalPointableElement(_grabbable);
                _handGrabInteractable.InjectRigidbody(GetComponent<Rigidbody>());
            }
            
            // Setup Rigidbody
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                // Freeze everything except Z rotation
                rb.constraints = RigidbodyConstraints.FreezePosition | 
                               RigidbodyConstraints.FreezeRotationX | 
                               RigidbodyConstraints.FreezeRotationY;
            }
            
            // Subscribe to events
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
                    OnGrabStart();
                    break;
                    
                case PointerEventType.Unselect:
                    OnGrabEnd();
                    break;
            }
        }
        
        private void OnGrabStart()
        {
            _isLocallyGrabbing = true;
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(true);
            }
            
            Debug.Log($"[Lever {_leverLetter}] Grab started at angle {GetCurrentAngle():F1}°");
        }
        
        private void OnGrabEnd()
        {
            _isLocallyGrabbing = false;
            
            float finalAngle = GetCurrentAngle();
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(false);
                RPC_UpdateAngle(finalAngle);
            }
            
            Debug.Log($"[Lever {_leverLetter}] Grab ended at angle {finalAngle:F1}°");
        }
        
        private float GetCurrentAngle()
        {
            return _leverPivot.localEulerAngles.z;
        }
        
        private void ApplyRotation(float angle)
        {
            _leverPivot.localRotation = Quaternion.Euler(0, 0, angle);
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
            
            if (_puzzleController != null)
            {
                _puzzleController.RegisterLever(this, _leverIndex);
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            if (IsGrabbed && _isLocallyGrabbing)
            {
                // Read current angle from the transform (OneGrabRotateTransformer is handling it)
                NetworkedAngle = GetCurrentAngle();
            }
            
            // Check activation with POSITIVE angles
            // Activation happens when angle is LESS THAN OR EQUAL to 55°
            bool shouldBeActive = NetworkedAngle <= _activationAngle;
            
            if (shouldBeActive != _wasActivated)
            {
                IsActivated = shouldBeActive;
                _wasActivated = shouldBeActive;
                
                if (_puzzleController != null)
                {
                    _puzzleController.OnLeverStateChanged(_leverIndex, IsActivated);
                }
                
                RPC_NotifyActivation(IsActivated);
                
                Debug.Log($"[Lever {_leverLetter}] Activation changed to {IsActivated} at angle {NetworkedAngle:F1}°");
            }
        }
        
        public override void Render()
        {
            if (!_isLocallyGrabbing)
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
        
        public void ResetLever()
        {
            if (HasStateAuthority)
            {
                NetworkedAngle = _restAngle;
                IsActivated = false;
                IsGrabbed = false;
                _wasActivated = false;
            }
            
            ApplyRotation(_restAngle);
        }
        
        // Public accessors
        public int GetLeverIndex() => _leverIndex;
        public string GetLeverLetter() => _leverLetter;
        public bool GetIsActivated() => IsActivated;
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (_leverPivot == null) return;
            
            Vector3 center = _leverPivot.position;
            
            // Rest angle (120° - Red)
            Gizmos.color = Color.red;
            Vector3 restDir = Quaternion.Euler(0, 0, _restAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + restDir * 0.5f);
            
            // Activation angle (55° - Yellow)
            Gizmos.color = Color.yellow;
            Vector3 activateDir = Quaternion.Euler(0, 0, _activationAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + activateDir * 0.5f);
            
            // Current angle (Green if active)
            Gizmos.color = IsActivated ? Color.green : Color.magenta;
            Vector3 currentDir = Quaternion.Euler(0, 0, GetCurrentAngle()) * Vector3.up;
            Gizmos.DrawLine(center, center + currentDir * 0.7f);
        }
        
        #endregion
    }
}
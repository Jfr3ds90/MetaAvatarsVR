using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.Events;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkedLever : NetworkBehaviour, ITransformer
    {
        [Header("Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        
        [Header("Angles")]
        [SerializeField] private float _restAngle = 120f;
        [SerializeField] private float _activationAngle = 55f;
        [SerializeField] private float _minAngle = 45f;
        [SerializeField] private float _maxAngle = 130f;
        
        [Header("References")]
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Events")]
        public UnityEvent<string> OnLeverActivated;
        public UnityEvent<string> OnLeverDeactivated;
        
        // Network State
        [Networked] public float NetworkedAngle { get; set; }
        [Networked] public NetworkBool IsActivated { get; set; }
        [Networked] public NetworkBool IsGrabbed { get; set; }
        
        // Components
        private IGrabbable _grabbable;
        private Vector3 _fixedPosition;
        private Quaternion _fixedRotationXY;
        
        // Grab state
        private bool _isTransforming = false;
        private float _grabStartAngle;
        private Vector3 _grabStartDirection;
        private bool _wasActivated = false;
        
        private void Awake()
        {
            // Guardar posición fija y rotación XY
            _fixedPosition = transform.position;
            _fixedRotationXY = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0);
            
            SetupComponents();
            ApplyAngle(_restAngle);
        }
        
        private void SetupComponents()
        {
            // Configurar Rigidbody para que NO se mueva
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            
            // Agregar Grabbable
            var grabbable = GetComponent<Grabbable>();
            if (grabbable == null)
            {
                grabbable = gameObject.AddComponent<Grabbable>();
            }
            
            // Inyectar este script como transformer
            grabbable.InjectOptionalOneGrabTransformer(this);
            grabbable.InjectOptionalTargetTransform(transform);
            
            // Agregar HandGrabInteractable
            var handGrab = GetComponent<HandGrabInteractable>();
            if (handGrab == null)
            {
                handGrab = gameObject.AddComponent<HandGrabInteractable>();
                handGrab.InjectOptionalPointableElement(grabbable);
                handGrab.InjectRigidbody(rb);
            }
        }
        
        #region ITransformer Implementation
        
        public IGrabbable Grabbable => _grabbable;
        
        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
        }
        
        public void BeginTransform()
        {
            _isTransforming = true;
            _grabStartAngle = GetCurrentAngle();
            
            // Obtener dirección inicial desde el pivot a la mano
            if (_grabbable.GrabPoints.Count > 0)
            {
                Vector3 handPos = _grabbable.GrabPoints[0].position;
                _grabStartDirection = (handPos - transform.position).normalized;
                _grabStartDirection.z = 0; // Proyectar en plano XY
            }
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(true);
            }
        }
        
        public void UpdateTransform()
        {
            if (!_isTransforming) return;
            
            // FORZAR posición fija - NO permitir movimiento
            transform.position = _fixedPosition;
            
            // Calcular rotación basada en la mano
            if (_grabbable.GrabPoints.Count > 0)
            {
                Vector3 handPos = _grabbable.GrabPoints[0].position;
                Vector3 currentDirection = (handPos - transform.position).normalized;
                currentDirection.z = 0;
                
                // Calcular ángulo entre direcciones
                float angle = Vector3.SignedAngle(_grabStartDirection, currentDirection, Vector3.forward);
                float targetAngle = Mathf.Clamp(_grabStartAngle + angle, _minAngle, _maxAngle);
                
                ApplyAngle(targetAngle);
            }
        }
        
        public void EndTransform()
        {
            _isTransforming = false;
            
            // FORZAR posición fija una vez más
            transform.position = _fixedPosition;
            
            if (Runner && Runner.IsRunning)
            {
                RPC_SetGrabbed(false);
                RPC_UpdateAngle(GetCurrentAngle());
            }
        }
        
        #endregion
        
        private void ApplyAngle(float angle)
        {
            // Mantener X e Y fijos, solo rotar en Z
            transform.rotation = _fixedRotationXY * Quaternion.Euler(0, 0, angle);
        }
        
        private float GetCurrentAngle()
        {
            return transform.localEulerAngles.z;
        }
        
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                NetworkedAngle = _restAngle;
                IsActivated = false;
            }
            
            ApplyAngle(NetworkedAngle);
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            // Forzar posición incluso en red
            transform.position = _fixedPosition;
            
            if (IsGrabbed && _isTransforming)
            {
                NetworkedAngle = GetCurrentAngle();
            }
            
            bool shouldBeActive = NetworkedAngle <= _activationAngle;
            if (shouldBeActive != _wasActivated)
            {
                IsActivated = shouldBeActive;
                _wasActivated = shouldBeActive;
                RPC_NotifyActivation(IsActivated);
            }
        }
        
        public override void Render()
        {
            // Siempre forzar posición
            transform.position = _fixedPosition;
            
            if (!_isTransforming)
            {
                ApplyAngle(Mathf.LerpAngle(GetCurrentAngle(), NetworkedAngle, Time.deltaTime * 10f));
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetGrabbed(NetworkBool grabbed) => IsGrabbed = grabbed;
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_UpdateAngle(float angle) => NetworkedAngle = angle;
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyActivation(NetworkBool activated)
        {
            if (activated) OnLeverActivated?.Invoke(_leverLetter);
            else OnLeverDeactivated?.Invoke(_leverLetter);
        }
        
        // Accessors
        public int GetLeverIndex() => _leverIndex;
        public string GetLeverLetter() => _leverLetter;
    }
}
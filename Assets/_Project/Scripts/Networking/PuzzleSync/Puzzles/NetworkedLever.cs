using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    /// <summary>
    /// NetworkedLever con sistema anti-oscilación mejorado
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkedMetaGrabbable))]
    public class NetworkedLever : NetworkBehaviour
    {
        [Header("Lever Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        [SerializeField] private float _restingAngle = -120f;
        [SerializeField] private float _activationAngle = -55f;
        [SerializeField] private float _maxUpAngle = -45f;
        [SerializeField] private Transform _leverHandle;
        [SerializeField] private bool _returnToCenter = false;
        [SerializeField] private float _rotationSpeed = 5f;
        
        [Header("Puzzle Integration")]
        [SerializeField] private NetworkedLeverPuzzle _puzzleController;
        
        [Header("Meta SDK Components")]
        private NetworkedMetaGrabbable _metaGrabbable;
        private Grabbable _grabbable;
        private OneGrabRotateTransformer _rotateTransformer;
        
        [Header("Visual & Audio")]
        [SerializeField] private Material _defaultMaterial;
        [SerializeField] private Material _activatedMaterial;
        [SerializeField] private Material _hoverMaterial;
        [SerializeField] private AudioClip _grabSound;
        [SerializeField] private AudioClip _releaseSound;
        [SerializeField] private AudioClip _activationSound;
        [SerializeField] private GameObject _activatedIndicator;
        [SerializeField] private ParticleSystem _activationParticles;
        
        [Header("Network State")]
        [Networked, OnChangedRender(nameof(OnNetworkStateChanged))]
        public NetworkBool IsActivated { get; set; }
        
        [Networked]
        public int ActivationOrder { get; set; }
        
        [Networked]
        public NetworkBool IsGrabbed { get; set; }
        
        [Networked]
        public PlayerRef CurrentGrabbingPlayer { get; set; }
        
        [Networked, OnChangedRender(nameof(OnNetworkRotationChanged))]
        public QuaternionCompressed NetworkedRotation { get; set; }
        
        [Networked]
        public TickTimer InteractionCooldown { get; set; }
        
        [Header("Lever Events")]
        public UnityEvent<int, bool> OnLeverStateChanged = new UnityEvent<int, bool>();
        public UnityEvent<string> OnLeverActivated = new UnityEvent<string>();
        public UnityEvent<string> OnLeverDeactivated = new UnityEvent<string>();
        public UnityEvent OnLeverGrabbed = new UnityEvent();
        public UnityEvent OnLeverReleased = new UnityEvent();
        
        // Rotation Control
        private enum RotationControlMode
        {
            NetworkControlled,
            LocallyControlled,
            Returning
        }
        
        private RotationControlMode _currentControlMode = RotationControlMode.NetworkControlled;
        private Quaternion _startRotation;
        private Quaternion _lastNetworkRotation;
        private Quaternion _rotationVelocity;
        private MeshRenderer _meshRenderer;
        private AudioSource _audioSource;
        private bool _isProcessingRelease = false;
        private bool _isLocallyControlling = false;
        private float _smoothTime = 0.1f;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Cache componentes
            _meshRenderer = GetComponent<MeshRenderer>();
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            
            // Obtener componentes Meta
            _metaGrabbable = GetComponent<NetworkedMetaGrabbable>();
            _grabbable = GetComponent<Grabbable>();
            _rotateTransformer = GetComponent<OneGrabRotateTransformer>();
            
            if (_leverHandle == null)
                _leverHandle = transform;
            
            // Establecer rotación inicial
            _leverHandle.localRotation = Quaternion.Euler(0, 0, _restingAngle);
            _startRotation = Quaternion.Euler(0, 0, _restingAngle);
            _lastNetworkRotation = _startRotation;
            
            // Auto-buscar puzzle controller si no está asignado
            if (_puzzleController == null)
                _puzzleController = GetComponentInParent<NetworkedLeverPuzzle>();
        }
        
        private void Start()
        {
            SetupMetaInteractionEvents();
            ConfigureRotationConstraints();
        }
        
        #endregion
        
        #region Network Lifecycle
        
        public override void Spawned()
        {
            Debug.Log($"[NetworkedLever] Spawned - Lever {_leverIndex} ({_leverLetter})");
            
            // Deshabilitar física en clientes
            if (!HasStateAuthority)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                }
            }
            
            if (HasStateAuthority)
            {
                IsActivated = false;
                ActivationOrder = -1;
                IsGrabbed = false;
                NetworkedRotation = _startRotation;
            }
            
            // Registrar con el puzzle controller
            if (_puzzleController != null)
            {
                _puzzleController.RegisterLever(this, _leverIndex);
            }
            
            // Configurar estado visual inicial
            UpdateLeverVisual(IsActivated);
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            // Actualizar rotación networked constantemente en el host
            NetworkedRotation = _leverHandle.localRotation;
            
            // Procesar return to center si está configurado
            if (!IsGrabbed && _returnToCenter && !IsActivated)
            {
                ProcessReturnToCenter();
            }
        }
        
        public override void Render()
        {
            // Sistema de interpolación basado en modo de control
            if (!HasStateAuthority)
            {
                switch (_currentControlMode)
                {
                    case RotationControlMode.NetworkControlled:
                        // Interpolar suavemente hacia rotación de red
                        _leverHandle.localRotation = SmoothDampQuaternion(
                            _leverHandle.localRotation,
                            _lastNetworkRotation,
                            ref _rotationVelocity,
                            _smoothTime
                        );
                        break;
                        
                    case RotationControlMode.LocallyControlled:
                        // No interpolar - el jugador local está controlando
                        break;
                        
                    case RotationControlMode.Returning:
                        // Retornar a posición de descanso
                        _leverHandle.localRotation = Quaternion.Slerp(
                            _leverHandle.localRotation,
                            _startRotation,
                            Time.deltaTime * 3f
                        );
                        
                        if (Quaternion.Angle(_leverHandle.localRotation, _startRotation) < 1f)
                        {
                            _currentControlMode = RotationControlMode.NetworkControlled;
                        }
                        break;
                }
            }
        }
        
        #endregion
        
        #region Meta SDK Integration
        
        private void SetupMetaInteractionEvents()
        {
            Debug.Log($"[NetworkedLever] Setting up Meta interaction events for lever {_leverIndex}");
            
            // Conectar con NetworkedMetaGrabbable
            if (_metaGrabbable != null)
            {
                _metaGrabbable.OnMetaGrabbed.RemoveAllListeners();
                _metaGrabbable.OnMetaReleased.RemoveAllListeners();
                
                _metaGrabbable.OnMetaGrabbed.AddListener(OnMetaGrabbed);
                _metaGrabbable.OnMetaReleased.AddListener(OnMetaReleased);
                _metaGrabbable.OnMetaHovered.AddListener(OnMetaHovered);
                _metaGrabbable.OnMetaUnhovered.AddListener(OnMetaUnhovered);
                
                Debug.Log($"[NetworkedLever] Connected to NetworkedMetaGrabbable");
            }
            else
            {
                Debug.LogError($"[NetworkedLever] NetworkedMetaGrabbable NOT FOUND on lever {_leverIndex}!");
            }
            
            // Eventos directos del Grabbable para feedback local
            if (_grabbable != null)
            {
                _grabbable.WhenPointerEventRaised += OnPointerEvent;
            }
        }
        
        private void ConfigureRotationConstraints()
        {
            if (_rotateTransformer != null)
            {
                /*_rotateTransformer.Constraints = new OneGrabRotateTransformer.OneGrabRotateConstraints
                {
                    MinAngle = new OneGrabRotateTransformer.OneGrabRotateConstraints.FloatConstraint
                    {
                        Constrain = true,
                        Value = _restingAngle
                    },
                    MaxAngle = new OneGrabRotateTransformer.OneGrabRotateConstraints.FloatConstraint
                    {
                        Constrain = true,
                        Value = _maxUpAngle
                    }
                };*/

                _rotateTransformer.Constraints = new OneGrabRotateTransformer.OneGrabRotateConstraints
                {
                    MinAngle = new FloatConstraint
                    {
                        Value = _restingAngle,
                        Constrain = true,
                    },
                    MaxAngle = new FloatConstraint
                    {
                        Value = _maxUpAngle,
                        Constrain = true,
                    }
                };
            }
        }
        
        private void OnPointerEvent(PointerEvent evt)
        {
            // Feedback visual local inmediato
            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    ShowHoverFeedback(true);
                    break;
                    
                case PointerEventType.Unhover:
                    ShowHoverFeedback(false);
                    break;
                    
                case PointerEventType.Select:
                    PlayGrabFeedback();
                    break;
            }
        }
        
        #endregion
        
        #region Interaction Handlers
        
        private void OnMetaGrabbed(PlayerRef player)
        {
            Debug.Log($"[NetworkedLever] OnMetaGrabbed - Lever {_leverIndex} by player {player}");
            
            if (Runner != null && Runner.IsRunning)
            {
                RPC_OnLeverGrabbed(Runner.LocalPlayer);
            }
        }
        
        private void OnMetaReleased(PlayerRef player)
        {
            Debug.Log($"[NetworkedLever] OnMetaReleased - Lever {_leverIndex} by player {player}");
            
            if (Runner != null && Runner.IsRunning)
            {
                RPC_OnLeverReleased(Runner.LocalPlayer);
            }
        }
        
        private void OnMetaHovered(PlayerRef player)
        {
            if (Runner != null && Runner.IsRunning)
            {
                RPC_OnLeverHovered(Runner.LocalPlayer, true);
            }
        }
        
        private void OnMetaUnhovered(PlayerRef player)
        {
            if (Runner != null && Runner.IsRunning)
            {
                RPC_OnLeverHovered(Runner.LocalPlayer, false);
            }
        }
        
        #endregion
        
        #region RPCs
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnLeverGrabbed(PlayerRef player, RpcInfo info = default)
        {
            // Validación de cooldown
            if (InteractionCooldown.ExpiredOrNotRunning(Runner) == false)
            {
                Debug.Log($"[NetworkedLever] Lever {_leverIndex} on cooldown");
                return;
            }
            
            // Validación de estado
            if (IsGrabbed)
            {
                Debug.Log($"[NetworkedLever] Lever {_leverIndex} already grabbed");
                return;
            }
            
            IsGrabbed = true;
            CurrentGrabbingPlayer = player;
            _isProcessingRelease = false;
            
            // Establecer cooldown
            InteractionCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
            
            // Notificar a todos del cambio de control
            RPC_NotifyControlChange(player, true);
            
            Debug.Log($"[NetworkedLever] Lever {_leverIndex} grabbed by player {player}");
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_OnLeverReleased(PlayerRef player, RpcInfo info = default)
        {
            if (!IsGrabbed || CurrentGrabbingPlayer != player)
                return;
            
            if (_isProcessingRelease)
                return;
                
            _isProcessingRelease = true;
            IsGrabbed = false;
            
            // Obtener el ángulo actual
            float currentAngleZ = _leverHandle.localRotation.eulerAngles.z;
            if (currentAngleZ > 180) currentAngleZ -= 360;
            
            // Clampear dentro del rango permitido
            currentAngleZ = Mathf.Clamp(currentAngleZ, _restingAngle, _maxUpAngle);
            
            // Verificar activación
            bool shouldBeActive = currentAngleZ >= _activationAngle;
            
            if (shouldBeActive != IsActivated)
            {
                IsActivated = shouldBeActive;
                
                // Notificar al puzzle controller
                if (_puzzleController != null)
                {
                    if (IsActivated)
                    {
                        ActivationOrder = _puzzleController.RegisterLeverActivation(_leverIndex);
                        _puzzleController.OnLeverStateChanged(_leverIndex, true);
                    }
                    else
                    {
                        _puzzleController.OnLeverStateChanged(_leverIndex, false);
                        ActivationOrder = -1;
                    }
                }
                
                RPC_BroadcastActivationState(IsActivated);
            }
            
            // Notificar cambio de control
            RPC_NotifyControlChange(player, false);
            
            Debug.Log($"[NetworkedLever] Lever {_leverIndex} released at angle {currentAngleZ:F1}°. Activated: {IsActivated}");
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyControlChange(PlayerRef controllingPlayer, bool isGrabbing)
        {
            if (isGrabbing)
            {
                // Alguien tomó control
                if (controllingPlayer == Runner.LocalPlayer)
                {
                    _currentControlMode = RotationControlMode.LocallyControlled;
                   _isLocallyControlling = true;
                   Debug.Log($"[NetworkedLever] Lever {_leverIndex} - Taking local control");
               }
               else
               {
                   _currentControlMode = RotationControlMode.NetworkControlled;
                   _isLocallyControlling = false;
                   Debug.Log($"[NetworkedLever] Lever {_leverIndex} - Remote player has control");
               }
               
               OnLeverGrabbed?.Invoke();
               PlayGrabFeedback();
           }
           else
           {
               // Se liberó el control
               _isLocallyControlling = false;
               
               if (_returnToCenter && !IsActivated)
               {
                   _currentControlMode = RotationControlMode.Returning;
               }
               else
               {
                   _currentControlMode = RotationControlMode.NetworkControlled;
               }
               
               OnLeverReleased?.Invoke();
               PlayReleaseFeedback();
           }
       }
       
       [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
       private void RPC_OnLeverHovered(PlayerRef player, NetworkBool isHovering, RpcInfo info = default)
       {
           // Podemos trackear hovering si es necesario
           RPC_BroadcastHoverFeedback(isHovering);
       }
       
       [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
       private void RPC_BroadcastActivationState(NetworkBool activated)
       {
           OnLeverStateChanged?.Invoke(_leverIndex, activated);
           
           if (activated)
           {
               OnLeverActivated?.Invoke(_leverLetter);
               PlayActivationFeedback();
               ShowActivationEffects();
           }
           else
           {
               OnLeverDeactivated?.Invoke(_leverLetter);
               PlayDeactivationFeedback();
           }
           
           UpdateLeverVisual(activated);
       }
       
       [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
       private void RPC_BroadcastHoverFeedback(NetworkBool isHovering)
       {
           ShowHoverFeedback(isHovering);
       }
       
       [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
       public void RPC_ResetLever()
       {
           IsActivated = false;
           ActivationOrder = -1;
           IsGrabbed = false;
           NetworkedRotation = _startRotation;
           _isProcessingRelease = false;
           _currentControlMode = RotationControlMode.NetworkControlled;
           
           UpdateLeverVisual(false);
           
           // Resetear rotación
           if (_leverHandle != null)
           {
               _leverHandle.localRotation = _startRotation;
               _lastNetworkRotation = _startRotation;
           }
       }
       
       #endregion
       
       #region Visual & Audio Feedback
       
       private void UpdateLeverVisual(bool activated)
       {
           /*if (_meshRenderer != null)
           {
               _meshRenderer.material = activated ? _activatedMaterial : _defaultMaterial;
           }
           
           if (_activatedIndicator != null)
           {
               _activatedIndicator.SetActive(activated);
           }*/
       }
       
       private void ShowHoverFeedback(bool hovering)
       {
           /*if (_meshRenderer != null && _hoverMaterial != null && !IsGrabbed)
           {
               _meshRenderer.material = hovering ? _hoverMaterial : 
                   (IsActivated ? _activatedMaterial : _defaultMaterial);
           }*/
       }
       
       private void ShowActivationEffects()
       {
           if (_activationParticles != null)
           {
               _activationParticles.Play();
           }
       }
       
       private void PlayGrabFeedback()
       {
           if (_audioSource != null && _grabSound != null)
           {
               _audioSource.PlayOneShot(_grabSound, 0.7f);
           }
       }
       
       private void PlayReleaseFeedback()
       {
           if (_audioSource != null && _releaseSound != null)
           {
               _audioSource.PlayOneShot(_releaseSound, 0.5f);
           }
       }
       
       private void PlayActivationFeedback()
       {
           if (_audioSource != null && _activationSound != null)
           {
               _audioSource.pitch = 1.2f;
               _audioSource.PlayOneShot(_activationSound);
               _audioSource.pitch = 1f;
           }
       }
       
       private void PlayDeactivationFeedback()
       {
           if (_audioSource != null && _activationSound != null)
           {
               _audioSource.pitch = 0.8f;
               _audioSource.PlayOneShot(_activationSound, 0.6f);
               _audioSource.pitch = 1f;
           }
       }
       
       #endregion
       
       #region Movement & Animation
       
       private void ProcessReturnToCenter()
       {
           float targetAngle = _restingAngle;
           float currentAngle = _leverHandle.localRotation.eulerAngles.z;
           if (currentAngle > 180) currentAngle -= 360;
           
           if (Mathf.Abs(currentAngle - targetAngle) > 0.1f)
           {
               float newAngle = Mathf.Lerp(currentAngle, targetAngle, Runner.DeltaTime * 2f);
               _leverHandle.localRotation = Quaternion.Euler(0, 0, newAngle);
               NetworkedRotation = _leverHandle.localRotation;
           }
       }
       
       // Callback cuando cambia la rotación de red
       private void OnNetworkRotationChanged()
       {
           // Solo actualizar si estamos en modo network-controlled
           if (_currentControlMode == RotationControlMode.NetworkControlled)
           {
               _lastNetworkRotation = NetworkedRotation;
           }
       }
       
       // Callback cuando cambia el estado de activación
       private void OnNetworkStateChanged()
       {
           UpdateLeverVisual(IsActivated);
       }
       
       // Método helper para smooth damp de quaterniones
       private Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, 
                                              ref Quaternion velocity, float smoothTime)
       {
           // Manejar el caso de quaterniones opuestos
           float dot = Quaternion.Dot(current, target);
           float multi = dot > 0 ? 1f : -1f;
           target.x *= multi;
           target.y *= multi;
           target.z *= multi;
           target.w *= multi;
           
           // Smooth damp cada componente
           Vector4 result = new Vector4(
               Mathf.SmoothDamp(current.x, target.x, ref velocity.x, smoothTime),
               Mathf.SmoothDamp(current.y, target.y, ref velocity.y, smoothTime),
               Mathf.SmoothDamp(current.z, target.z, ref velocity.z, smoothTime),
               Mathf.SmoothDamp(current.w, target.w, ref velocity.w, smoothTime)
           ).normalized;
           
           velocity = new Quaternion(velocity.x, velocity.y, velocity.z, velocity.w);
           return new Quaternion(result.x, result.y, result.z, result.w);
       }
       
       #endregion
       
       #region Public Methods
       
       /// <summary>
       /// Resetea la palanca a su estado inicial
       /// </summary>
       public void ResetLever()
       {
           if (HasStateAuthority)
           {
               RPC_ResetLever();
           }
       }
       
       /// <summary>
       /// Obtiene el índice de la palanca
       /// </summary>
       public int GetLeverIndex()
       {
           return _leverIndex;
       }
       
       /// <summary>
       /// Obtiene la letra asignada a la palanca
       /// </summary>
       public string GetLeverLetter()
       {
           return _leverLetter;
       }
       
       /// <summary>
       /// Establece los datos de la palanca
       /// </summary>
       public void SetLeverData(int index, string letter)
       {
           _leverIndex = index;
           _leverLetter = letter;
       }
       
       /// <summary>
       /// Establece el controlador del puzzle
       /// </summary>
       public void SetPuzzleController(NetworkedLeverPuzzle controller)
       {
           _puzzleController = controller;
       }
       
       #endregion
       
       #region Gizmos
       
       private void OnDrawGizmosSelected()
       {
           if (_leverHandle == null)
               _leverHandle = transform;
           
           // Determinar el color basado en el estado
           Color gizmoColor = Color.red;
           
           // Solo acceder a IsActivated si el objeto está spawneado
           if (Application.isPlaying && Object != null && Object.IsValid)
           {
               gizmoColor = IsActivated ? Color.green : Color.red;
           }
           
           // Mostrar posición actual
           Gizmos.color = gizmoColor;
           Gizmos.DrawWireCube(_leverHandle.position, Vector3.one * 0.2f);
           
           // Mostrar dirección de rotación
           Vector3 direction = _leverHandle.rotation * Vector3.forward;
           Gizmos.DrawRay(_leverHandle.position, direction * 0.5f);
           
           // Mostrar ángulo de activación
           Gizmos.color = Color.yellow;
           Vector3 activatedDirection = Quaternion.Euler(0, 0, _activationAngle) * Vector3.forward;
           Gizmos.DrawRay(_leverHandle.position, activatedDirection * 0.5f);
           
           // Mostrar información de debug
           #if UNITY_EDITOR
           string debugInfo = $"Lever {_leverIndex} ({_leverLetter})";
           
           // Solo mostrar información de runtime si está spawneado
           if (Application.isPlaying && Object != null && Object.IsValid)
           {
               float currentAngle = _leverHandle.localRotation.eulerAngles.z;
               if (currentAngle > 180) currentAngle -= 360;
               debugInfo += $"\nAngle: {currentAngle:F1}°\nActive: {IsActivated}";
               debugInfo += $"\nControl: {_currentControlMode}";
           }
           else
           {
               debugInfo += "\n[Not Spawned]";
           }
           
           UnityEditor.Handles.Label(
               _leverHandle.position + Vector3.up * 0.3f, 
               debugInfo
           );
           #endif
       }
       
       #endregion
   }
}
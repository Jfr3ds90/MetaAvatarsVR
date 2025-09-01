using Fusion;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaAvatarsVR.Networking.PuzzleSync.Puzzles
{
    /// <summary>
    /// NetworkedLever con sistema de rotación corregido
    /// Movimiento correcto: -120° (abajo/reposo) → -55° (arriba/activado)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkedMetaGrabbable))]
    public class NetworkedLever : NetworkBehaviour
    {
        [Header("Lever Configuration")]
        [SerializeField] private int _leverIndex;
        [SerializeField] private string _leverLetter = "A";
        
        [Header("Angular Configuration - FIXED")]
        [SerializeField, Tooltip("Ángulo de reposo (palanca abajo)")]
        private float _restingAngle = -120f;  // Posición inicial - palanca abajo
        
        [SerializeField, Tooltip("Ángulo de activación")]
        private float _activationAngle = -55f;  // Umbral de activación
        
        [SerializeField, Tooltip("Ángulo máximo permitido")]
        private float _maxUpAngle = -45f;  // Límite superior del movimiento
        
        [SerializeField, Tooltip("Tolerancia para activación en grados")]
        private float _activationTolerance = 5f;
        
        [SerializeField] private Transform _leverHandle;
        [SerializeField] private bool _returnToCenter = false;
        [SerializeField] private float _returnSpeed = 2f;
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
        public float NetworkedAngle { get; set; }  // Añadido para mejor debug
        
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
        private bool _wasActivatedLastFrame = false;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            ValidateAngularConfiguration();
            
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
            
            // Establecer rotación inicial CORRECTA
            SetLeverRotation(_restingAngle);
            _startRotation = Quaternion.Euler(0, 0, _restingAngle);
            _lastNetworkRotation = _startRotation;
            
            // Auto-buscar puzzle controller si no está asignado
            if (_puzzleController == null)
                _puzzleController = GetComponentInParent<NetworkedLeverPuzzle>();
                
            Debug.Log($"[NetworkedLever] Initialized Lever {_leverIndex}: Rest={_restingAngle}°, Activation={_activationAngle}°, Max={_maxUpAngle}°");
        }
        
        private void Start()
        {
            SetupMetaInteractionEvents();
            ConfigureRotationConstraints();
        }
        
        /// <summary>
        /// Valida que la configuración angular tenga sentido
        /// </summary>
        private void ValidateAngularConfiguration()
        {
            if (!(_restingAngle <= _activationAngle && _activationAngle <= _maxUpAngle))
            {
                Debug.LogError($"[NetworkedLever] Invalid angular configuration! " +
                    $"Expected: Rest({_restingAngle}) <= Activation({_activationAngle}) <= Max({_maxUpAngle})");
            }
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
                NetworkedAngle = _restingAngle;
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
            
            // Actualizar rotación y ángulo en red
            NetworkedRotation = _leverHandle.localRotation;
            NetworkedAngle = GetCurrentAngle();
            
            // Verificar cambios de estado de activación mientras está agarrada
            if (IsGrabbed)
            {
                CheckActivationWhileGrabbed();
            }
            
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
                            Time.deltaTime * _returnSpeed
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
                // Configurar constraints correctamente
                // Para rotación en Z con movimiento de -120° a -45°
                _rotateTransformer.Constraints = new OneGrabRotateTransformer.OneGrabRotateConstraints
                {
                    MinAngle = new FloatConstraint
                    {
                        Value = _restingAngle,  // -120° (abajo)
                        Constrain = true,
                    },
                    MaxAngle = new FloatConstraint
                    {
                        Value = _maxUpAngle,    // -45° (arriba)
                        Constrain = true,
                    }
                };
                
                Debug.Log($"[NetworkedLever] Rotation constraints set: Min={_restingAngle}°, Max={_maxUpAngle}°");
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
        
        #region Angular Calculations - FIXED
        
        /// <summary>
        /// Obtiene el ángulo actual normalizado de la palanca
        /// </summary>
        private float GetCurrentAngle()
        {
            float angle = _leverHandle.localRotation.eulerAngles.z;
            
            // Normalizar al rango [-180, 180]
            if (angle > 180f) 
                angle -= 360f;
            
            return angle;
        }
        
        /// <summary>
        /// Establece la rotación de la palanca a un ángulo específico
        /// </summary>
        private void SetLeverRotation(float angle)
        {
            _leverHandle.localRotation = Quaternion.Euler(0, 0, angle);
        }
        
        /// <summary>
        /// Verifica si la palanca está en posición de activación
        /// </summary>
        private bool CheckIfActivated(float currentAngle)
        {
            // La palanca está activada cuando el ángulo es >= -55° (más cercano a 0)
            // Considerando la tolerancia
            return currentAngle >= (_activationAngle - _activationTolerance);
        }
        
        /// <summary>
        /// Clampea el ángulo dentro de los límites permitidos
        /// </summary>
        private float ClampAngle(float angle)
        {
            // min debe ser el valor más pequeño (-120), max el más grande (-45)
            return Mathf.Clamp(angle, _restingAngle, _maxUpAngle);
        }
        
        /// <summary>
        /// Verifica cambios de activación mientras la palanca está agarrada
        /// </summary>
        private void CheckActivationWhileGrabbed()
        {
            float currentAngle = GetCurrentAngle();
            bool shouldBeActive = CheckIfActivated(currentAngle);
            
            // Detectar cambio de estado
            if (shouldBeActive != _wasActivatedLastFrame)
            {
                if (shouldBeActive)
                {
                    Debug.Log($"[NetworkedLever] Lever {_leverIndex} crossed activation threshold at {currentAngle:F1}°");
                    // Podemos añadir feedback inmediato aquí si queremos
                }
                _wasActivatedLastFrame = shouldBeActive;
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
            _wasActivatedLastFrame = IsActivated;
            
            // Establecer cooldown
            InteractionCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
            
            // Notificar a todos del cambio de control
            RPC_NotifyControlChange(player, true);
            
            float currentAngle = GetCurrentAngle();
            Debug.Log($"[NetworkedLever] Lever {_leverIndex} grabbed by player {player} at angle {currentAngle:F1}°");
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
            
            // Obtener y procesar el ángulo correctamente
            float currentAngle = GetCurrentAngle();
            
            // Clampear dentro del rango permitido
            currentAngle = ClampAngle(currentAngle);
            
            // Verificar activación con el método correcto
            bool shouldBeActive = CheckIfActivated(currentAngle);
            
            Debug.Log($"[NetworkedLever] Release Check - Angle: {currentAngle:F1}°, " +
                     $"Activation Threshold: {_activationAngle}°, " +
                     $"Should Activate: {shouldBeActive}");
            
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
            
            Debug.Log($"[NetworkedLever] Lever {_leverIndex} released at angle {currentAngle:F1}°. " +
                     $"Activated: {IsActivated} (threshold: {_activationAngle}°)");
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
            NetworkedAngle = _restingAngle;
            _isProcessingRelease = false;
            _wasActivatedLastFrame = false;
            _currentControlMode = RotationControlMode.NetworkControlled;
            
            UpdateLeverVisual(false);
            
            // Resetear rotación
            if (_leverHandle != null)
            {
                SetLeverRotation(_restingAngle);
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
            }*/
            
            if (_activatedIndicator != null)
            {
                _activatedIndicator.SetActive(activated);
            }
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
            float currentAngle = GetCurrentAngle();
            
            if (Mathf.Abs(currentAngle - _restingAngle) > 0.1f)
            {
                float newAngle = Mathf.Lerp(currentAngle, _restingAngle, Runner.DeltaTime * _returnSpeed);
                SetLeverRotation(newAngle);
                NetworkedRotation = _leverHandle.localRotation;
                NetworkedAngle = newAngle;
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
        /// Obtiene el ángulo actual de la palanca
        /// </summary>
        public float GetLeverAngle()
        {
            return GetCurrentAngle();
        }
        
        /// <summary>
        /// Obtiene el progreso de la palanca (0 = reposo, 1 = máximo)
        /// </summary>
        public float GetLeverProgress()
        {
            float current = GetCurrentAngle();
            float range = _maxUpAngle - _restingAngle;
            if (Mathf.Abs(range) < 0.01f) return 0f;
            
            return Mathf.Clamp01((current - _restingAngle) / range);
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
        
        #region Debug & Diagnostics
        
        /// <summary>
        /// Método de diagnóstico para verificar la configuración angular
        /// </summary>
        [ContextMenu("Diagnose Lever Configuration")]
        private void DiagnoseLeverConfiguration()
        {
            Debug.Log("=== LEVER ANGULAR DIAGNOSTIC ===");
            Debug.Log($"Lever {_leverIndex} ({_leverLetter})");
            Debug.Log($"Rest Angle: {_restingAngle}° (palanca abajo)");
            Debug.Log($"Activation Angle: {_activationAngle}° (umbral de activación)");
            Debug.Log($"Max Up Angle: {_maxUpAngle}° (límite superior)");
            
            float currentAngle = GetCurrentAngle();
            Debug.Log($"Current Angle: {currentAngle:F1}°");
            Debug.Log($"Progress: {GetLeverProgress():P0}");
            Debug.Log($"Would Activate: {CheckIfActivated(currentAngle)}");
            
            // Verificar configuración
            if (_restingAngle > _activationAngle)
            {
                Debug.LogError("ERROR: Rest angle is greater than activation angle! This will cause inverted behavior.");
            }
            
            if (_activationAngle > _maxUpAngle)
            {
                Debug.LogError("ERROR: Activation angle is greater than max angle! Lever can never activate.");
            }
            
            Debug.Log("=== END DIAGNOSTIC ===");
        }
        
        /// <summary>
        /// Test de movimiento de la palanca
        /// </summary>
        [ContextMenu("Test Lever Movement")]
        private void TestLeverMovement()
        {
            StartCoroutine(TestMovementSequence());
        }
        
        private System.Collections.IEnumerator TestMovementSequence()
        {
            Debug.Log("[TEST] Starting lever movement test...");
            
            // Test 1: Posición de reposo
            Debug.Log("[TEST] Moving to rest position...");
            SetLeverRotation(_restingAngle);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[TEST] At rest: {GetCurrentAngle():F1}°");
            
            // Test 2: Posición de activación
            Debug.Log("[TEST] Moving to activation threshold...");
            SetLeverRotation(_activationAngle);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[TEST] At activation: {GetCurrentAngle():F1}°, Should activate: {CheckIfActivated(GetCurrentAngle())}");
            
            // Test 3: Posición máxima
            Debug.Log("[TEST] Moving to max position...");
            SetLeverRotation(_maxUpAngle);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[TEST] At max: {GetCurrentAngle():F1}°");
            
            // Test 4: Movimiento gradual
            Debug.Log("[TEST] Performing gradual movement...");
            for (float t = 0; t <= 1f; t += 0.1f)
            {
                float angle = Mathf.Lerp(_restingAngle, _maxUpAngle, t);
                SetLeverRotation(angle);
                bool wouldActivate = CheckIfActivated(angle);
                Debug.Log($"[TEST] Progress {t:P0}: Angle={angle:F1}°, Activated={wouldActivate}");
                yield return new WaitForSeconds(0.3f);
            }
            
            // Return to rest
            Debug.Log("[TEST] Returning to rest...");
            SetLeverRotation(_restingAngle);
            Debug.Log("[TEST] Test complete!");
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmosSelected()
        {
            if (_leverHandle == null)
                _leverHandle = transform;
            
            Color gizmoColor = Color.red;
            float currentAngle = GetCurrentAngle();
            
            if (Application.isPlaying && Object != null && Object.IsValid)
            {
                gizmoColor = IsActivated ? Color.green : Color.red;
            }
            
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(_leverHandle.position, Vector3.one * 0.2f);
            
            Vector3 center = _leverHandle.position;
            float radius = 0.5f;
            
            DrawAngleArc(center, _restingAngle, _maxUpAngle, radius, Color.gray);
            
            Gizmos.color = Color.red;
            Vector3 restDirection = Quaternion.Euler(0, 0, _restingAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + restDirection * radius);
            DrawAngleLabel(center + restDirection * (radius + 0.1f), "REST", Color.red);
            
            Gizmos.color = Color.yellow;
            Vector3 activationDirection = Quaternion.Euler(0, 0, _activationAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + activationDirection * radius);
            DrawAngleLabel(center + activationDirection * (radius + 0.1f), "ACTIVATE", Color.yellow);
            
            Gizmos.color = Color.blue;
            Vector3 maxDirection = Quaternion.Euler(0, 0, _maxUpAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + maxDirection * radius);
            DrawAngleLabel(center + maxDirection * (radius + 0.1f), "MAX", Color.blue);
            
            Gizmos.color = Application.isPlaying && CheckIfActivated(currentAngle) ? Color.green : Color.magenta;
            Vector3 currentDirection = Quaternion.Euler(0, 0, currentAngle) * Vector3.up;
            Gizmos.DrawLine(center, center + currentDirection * (radius * 1.2f));
            
            #if UNITY_EDITOR
            string debugInfo = $"Lever {_leverIndex} ({_leverLetter})\n";
            debugInfo += $"Current: {currentAngle:F1}°\n";
            debugInfo += $"Rest: {_restingAngle}° | Act: {_activationAngle}° | Max: {_maxUpAngle}°\n";
            
            if (Application.isPlaying && Object != null && Object.IsValid)
            {
                debugInfo += $"Active: {IsActivated} | Grabbed: {IsGrabbed}\n";
                debugInfo += $"Progress: {GetLeverProgress():P0}\n";
                debugInfo += $"Control: {_currentControlMode}";
                
                if (_restingAngle > _activationAngle)
                {
                    debugInfo += "\n⚠️ INVERTED CONFIG!";
                }
            }
            else
            {
                debugInfo += "[Not Spawned]";
            }
            
            UnityEditor.Handles.Label(
                _leverHandle.position + Vector3.up * 0.6f, 
                debugInfo,
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                }
            );
            #endif
        }
        
        private void DrawAngleArc(Vector3 center, float startAngle, float endAngle, float radius, Color color)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            Vector3 from = Quaternion.Euler(0, 0, startAngle) * Vector3.up;
            float angle = endAngle - startAngle;
            UnityEditor.Handles.DrawWireArc(center, Vector3.forward, from, angle, radius);
            #endif
        }
        
        private void DrawAngleLabel(Vector3 position, string text, Color color)
        {
            #if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = color;
            style.fontSize = 9;
            style.fontStyle = FontStyle.Bold;
            UnityEditor.Handles.Label(position, text, style);
            #endif
        }
        
        private void OnDrawGizmos()
        {
            if (_leverHandle == null) return;
            
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(_leverHandle.position, 0.15f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                _leverHandle.position + Vector3.up * 0.25f, 
                _leverLetter,
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                }
            );
            #endif
        }
        
        #endregion
        
        #region Runtime Debug GUI
        
        private void OnGUI()
        {
            if (!Application.isEditor || !Object || !Object.IsValid) return;
            
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.D))
            {
                float currentAngle = GetCurrentAngle();
                
                GUI.Box(new Rect(10, 10 + (_leverIndex * 110), 250, 100), $"Lever {_leverIndex} ({_leverLetter})");
                GUI.Label(new Rect(15, 30 + (_leverIndex * 110), 240, 20), 
                    $"Angle: {currentAngle:F1}° | Network: {NetworkedAngle:F1}°");
                GUI.Label(new Rect(15, 50 + (_leverIndex * 110), 240, 20), 
                    $"Activated: {IsActivated} | Grabbed: {IsGrabbed}");
                GUI.Label(new Rect(15, 70 + (_leverIndex * 110), 240, 20), 
                    $"Progress: {GetLeverProgress():P0} | Mode: {_currentControlMode}");
                
                float progress = GetLeverProgress();
                GUI.color = CheckIfActivated(currentAngle) ? Color.green : Color.red;
                GUI.HorizontalSlider(new Rect(15, 90 + (_leverIndex * 110), 230, 20), 
                    progress, 0f, 1f);
                GUI.color = Color.white;
            }
        }
        
        #endregion
    }
}
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Oculus.Interaction;
using System.Collections;


namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Sistema de grab para VR con Fusion 2 usando NetworkRigidbody3D
    /// Soluciona el problema de "lucha entre posiciones" usando la arquitectura correcta
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkRigidbody3D))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public class FusionVRGrabbable : NetworkBehaviour, IStateAuthorityChanged
    {
        [Header("Grab Configuration")] [SerializeField]
        private bool _allowMultipleGrabbers = true;

        [SerializeField] private float _grabBreakDistance = 2f;
        [SerializeField] private LayerMask _grabbedLayer = -1;

        [Header("Physics")] [SerializeField] private bool _maintainVelocityOnRelease = true;
        [SerializeField] private float _throwForceMultiplier = 1.5f;

        [Header("Slot System")] [SerializeField]
        private bool _useSnapSlots = false;

        [SerializeField] private float _snapDistance = 0.3f;
        [SerializeField] private string _slotTag = "SnapSlot";

        [Header("Debug")] [SerializeField] private bool _debugMode = true;

        // Components
        private NetworkRigidbody3D _networkRigidbody;
        private Rigidbody _rigidbody;
        private Grabbable _metaGrabbable;
        private Collider[] _colliders;

        // Networked State
        [Networked, OnChangedRender(nameof(OnIsGrabbedChanged))]
        public NetworkBool IsGrabbed { get; set; }

        [Networked] public PlayerRef CurrentGrabber { get; set; }
        [Networked] public Vector3 GrabOffset { get; set; }
        [Networked] public Quaternion GrabRotationOffset { get; set; }

        [Networked, OnChangedRender(nameof(OnIsKinematicChanged))]
        public NetworkBool IsKinematic { get; set; }

        [Networked] public Vector3 LastVelocity { get; set; }
        [Networked] public Vector3 LastAngularVelocity { get; set; }

        // Local State
        private bool _isLocallyGrabbed = false;
        private Transform _localGrabber;
        private Vector3 _velocityTracker;
        private Vector3 _angularVelocityTracker;
        private int _originalLayer;
        private bool _wasKinematicBeforeGrab;

        // Authority Management
        private Coroutine _authorityRequestCoroutine;

        //helpers
        private GameObject _grabberProxy;
        private Transform _cachedGrabberTransform;


        #region Unity Lifecycle

        void Awake()
        {
            // Cachear componentes
            _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            _rigidbody = GetComponent<Rigidbody>();
            _metaGrabbable = GetComponent<Grabbable>();
            _colliders = GetComponentsInChildren<Collider>();

            _originalLayer = gameObject.layer;

            // Validar configuración
            ValidateSetup();
        }

        void Start()
        {
            // Suscribirse a eventos de Meta Grabbable
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised += OnMetaGrabbableEvent;
            }
        }

        #endregion

        #region Network Lifecycle

        public override void Spawned()
        {
            Debug.Log($"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== SPAWNED =====");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - StateAuthority: {Object.StateAuthority}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - InputAuthority: {Object.InputAuthority}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - AllowOverride: {Object.Flags.HasFlag(NetworkObjectFlags.AllowStateAuthorityOverride)}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Position: {transform.position}");

            // Re-cachear componentes por si Awake() no los encontró
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            if (_networkRigidbody == null)
                _networkRigidbody = GetComponent<NetworkRigidbody3D>();

            if (_rigidbody == null)
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Rigidbody STILL null after Spawned()!");
                return;
            }

            if (HasStateAuthority)
            {
                IsGrabbed = false;
                CurrentGrabber = PlayerRef.None;
                IsKinematic = _rigidbody.isKinematic;

                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Has authority, initialized network state");
            }

            _rigidbody.isKinematic = IsKinematic;

            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== SPAWNED END =====");
        }

        // Implementación de IStateAuthorityChanged
        public void StateAuthorityChanged()
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== STATE AUTHORITY CHANGED =====");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - New StateAuthority: {Object.StateAuthority}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - IsGrabbed: {IsGrabbed}, CurrentGrabber: {CurrentGrabber}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== STATE AUTHORITY CHANGED END =====");
        }

        public override void FixedUpdateNetwork()
        {
            // LOG PERIÓDICO para objetos agarrados
            if (IsGrabbed && Runner != null)
            {
                if (Time.frameCount % 60 == 0) // Cada ~1 segundo
                {
                    Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - PERIODIC UPDATE:");
                    Debug.Log($"  IsGrabbed: {IsGrabbed}, Grabber: {CurrentGrabber}");
                    Debug.Log($"  HasAuthority: {HasStateAuthority}, LocalGrabbed: {_isLocallyGrabbed}");
                    Debug.Log($"  Position: {transform.position}");

                    if (_localGrabber != null)
                    {
                        Debug.Log($"  Grabber Position: {_localGrabber.position}");
                        Debug.Log($"  GrabOffset: {GrabOffset}");
                    }
                }
            }
        }

        public override void Render()
        {
            if (_isLocallyGrabbed && _localGrabber != null)
            {
                ApplyGrabMovement();
                TrackVelocity();
                CheckBreakDistance();
            }
        }

        #endregion

        #region Grab Logic

        private void OnMetaGrabbableEvent(PointerEvent evt)
        {
            // ===== VALIDACIONES CON LOGS =====
            if (_rigidbody == null || _networkRigidbody == null)
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Components not initialized! RB:{_rigidbody != null}, NetRB:{_networkRigidbody != null}");
                return;
            }

            if (Runner == null || Object == null)
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Network components not ready! Runner:{Runner != null}, Object:{Object != null}");
                return;
            }

            // ===== LOG DE EVENTOS =====
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - EVENT: {evt.Type} | HasAuth: {HasStateAuthority} | IsGrabbed: {IsGrabbed} | CurrentGrabber: {CurrentGrabber}");

            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    Debug.Log(
                        $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Hover detected, caching grabber...");
                    CacheGrabberFromHover();
                    break;

                case PointerEventType.Select:
                    if (!_isLocallyGrabbed)
                    {
                        Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== SELECT START =====");
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Object Position: {transform.position}");
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - evt.Pose: pos={evt.Pose.position}, rot={evt.Pose.rotation.eulerAngles}");

                        // Intentar encontrar el grabber real
                        Transform grabberTransform = _cachedGrabberTransform ?? FindActiveGrabberForThisObject();

                        // Si no se encuentra, crear proxy
                        if (grabberTransform == null)
                        {
                            Debug.LogWarning(
                                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Real grabber not found, using proxy");
                            grabberTransform = CreateGrabberProxy(evt.Pose);
                        }

                        if (grabberTransform == null)
                        {
                            Debug.LogError(
                                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Failed to get/create grabber!");
                            return;
                        }

                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Grabber: {grabberTransform.name}, Position: {grabberTransform.position}");

                        // Hacer kinematic inmediatamente
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Setting kinematic=true and teleporting...");
                        _rigidbody.isKinematic = true;
                        _networkRigidbody.Teleport();

                        _isLocallyGrabbed = true;
                        _localGrabber = grabberTransform;

                        // Calcular offsets
                        GrabOffset = _localGrabber.InverseTransformPoint(transform.position);
                        GrabRotationOffset = Quaternion.Inverse(_localGrabber.rotation) * transform.rotation;

                        Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - GrabOffset: {GrabOffset}");
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - GrabRotOffset: {GrabRotationOffset.eulerAngles}");

                        if (!HasStateAuthority)
                        {
                            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Requesting authority...");
                            _authorityRequestCoroutine = StartCoroutine(
                                RequestAuthorityWithTimeout(evt.Pose.position, evt.Pose.rotation)
                            );
                        }
                        else
                        {
                            Debug.Log(
                                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Already have authority, applying grab immediately");
                            ApplyGrab();
                        }

                        Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== SELECT END =====");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - SELECT ignored, already locally grabbed");
                    }

                    break;

                case PointerEventType.Move:
                    // Actualizar proxy si existe
                    if (_grabberProxy != null && _isLocallyGrabbed)
                    {
                        _grabberProxy.transform.position = evt.Pose.position;
                        _grabberProxy.transform.rotation = evt.Pose.rotation;

                        if (_debugMode)
                        {
                            Debug.Log(
                                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Proxy updated: {evt.Pose.position}");
                        }
                    }

                    break;

                case PointerEventType.Unselect:
                    Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== UNSELECT =====");

                    if (_isLocallyGrabbed && HasStateAuthority)
                    {
                        Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Releasing object...");
                        ForceRelease();
                    }
                    else
                    {
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - UNSELECT ignored. LocalGrabbed:{_isLocallyGrabbed}, HasAuth:{HasStateAuthority}");
                    }

                    // Destruir proxy
                    if (_grabberProxy != null)
                    {
                        Destroy(_grabberProxy);
                        _grabberProxy = null;
                        Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Proxy destroyed");
                    }

                    break;

                case PointerEventType.Unhover:
                    _cachedGrabberTransform = null;
                    Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Unhover, cache cleared");
                    break;
            }
        }

        /// <summary>
        /// Busca el interactor activo que está agarrando este objeto específico
        /// </summary>
        private Transform FindActiveGrabberForThisObject()
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Searching for active grabber...");

            var handGrabbers = FindObjectsOfType<Oculus.Interaction.HandGrab.HandGrabInteractor>();
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Found {handGrabbers.Length} HandGrabInteractor(s)");

            foreach (var grabber in handGrabbers)
            {
                if (grabber.State == Oculus.Interaction.InteractorState.Select)
                {
                    var interactable = grabber.Interactable;

                    if (interactable != null && interactable.gameObject == gameObject)
                    {
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ✓ Found grabber: {grabber.name}");
                        return grabber.transform;
                    }
                }
            }

            Debug.LogWarning(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - No matching grabber found");
            return null;
        }


        private IEnumerator RequestAuthorityWithTimeout(Vector3 grabPos, Quaternion grabRot, float timeout = 2f)
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Authority request started. Current authority: {Object.StateAuthority}");

            Object.RequestStateAuthority();

            float elapsedTime = 0f;
            while (!HasStateAuthority && elapsedTime < timeout)
            {
                // Mantener kinematic durante espera
                _rigidbody.isKinematic = true;

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (HasStateAuthority)
            {
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ✓ Authority acquired after {elapsedTime:F3}s. New authority: {Object.StateAuthority}");
                ApplyGrab();
            }
            else
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ✗ Authority request TIMEOUT after {timeout}s! Authority still: {Object.StateAuthority}");

                _isLocallyGrabbed = false;
                _localGrabber = null;
                _rigidbody.isKinematic = IsKinematic;

                if (_metaGrabbable != null)
                {
                    _metaGrabbable.enabled = false;
                    _metaGrabbable.enabled = true;
                }
            }

            _authorityRequestCoroutine = null;
        }

        private void OnGrabStart(PointerEvent evt)
        {
            if (!Runner.IsRunning) return;

            if (evt.Data is IInteractorView interactor && interactor is MonoBehaviour mb)
            {
                _localGrabber = mb.transform;

                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Local grab started. HasStateAuthority: {HasStateAuthority}");

                // Calcular offsets basados en la posición actual
                GrabOffset = _localGrabber.InverseTransformPoint(transform.position);
                GrabRotationOffset = Quaternion.Inverse(_localGrabber.rotation) * transform.rotation;

                _isLocallyGrabbed = true;

                if (!HasStateAuthority)
                {
                    // Solicitar autoridad sin hacer cambios prematuros
                    if (_authorityRequestCoroutine != null)
                        StopCoroutine(_authorityRequestCoroutine);

                    _authorityRequestCoroutine = StartCoroutine(RequestAuthorityCoroutine());
                }
                else
                {
                    ApplyGrab();
                }
            }
        }

        private void CacheGrabberFromHover()
        {
            var handGrabbers = FindObjectsOfType<Oculus.Interaction.HandGrab.HandGrabInteractor>();

            foreach (var grabber in handGrabbers)
            {
                if (grabber.State == Oculus.Interaction.InteractorState.Hover)
                {
                    var interactable = grabber.Interactable;
                    if (interactable != null && interactable.gameObject == gameObject)
                    {
                        _cachedGrabberTransform = grabber.transform;
                        Debug.Log(
                            $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Cached grabber from hover: {grabber.name}");
                        return;
                    }
                }
            }
        }

        private void OnGrabEnd()
        {
            if (!_isLocallyGrabbed) return;

            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Local release");

            _isLocallyGrabbed = false;

            if (HasStateAuthority)
            {
                ReleaseObject();
            }

            _localGrabber = null;
        }

        private IEnumerator RequestAuthorityCoroutine()
        {
            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Requesting state authority... (Current: {Object.StateAuthority})");

            // Guardar posición actual antes de solicitar autoridad
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;

            Object.RequestStateAuthority();

            float timeout = 1f;
            float elapsed = 0f;

            // Esperar autoridad sin mover el objeto
            while (!HasStateAuthority && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (HasStateAuthority)
            {
                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] State authority acquired in {elapsed:F2}s!");

                // SOLUCIÓN CRÍTICA: Teleport después de obtener autoridad
                // Esto sincroniza la posición correctamente con todos los clientes
                if (_networkRigidbody != null)
                {
                    _networkRigidbody.Teleport(currentPos, currentRot);

                    if (_debugMode)
                        Debug.Log(
                            $"[FusionVRGrabbable] Teleported to sync position after authority transfer: {currentPos}");
                }

                // Pequeña espera para que el teleport se propague
                yield return null;

                // Ahora aplicar el grab
                ApplyGrab();
            }
            else
            {
                if (_debugMode)
                    Debug.LogWarning($"[FusionVRGrabbable] Failed to acquire authority after {timeout}s");

                // No pudimos obtener autoridad, cancelar el grab
                _isLocallyGrabbed = false;
                _localGrabber = null;
            }

            _authorityRequestCoroutine = null;
        }

        private void ApplyGrab()
        {
            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== APPLY GRAB START =====");
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Using pre-calculated offsets:");
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} -   GrabOffset: {GrabOffset}");
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} -   GrabRotOffset: {GrabRotationOffset.eulerAngles}");
            }
    
            _wasKinematicBeforeGrab = _rigidbody.isKinematic;
    
            // Hacer kinematic
            _rigidbody.isKinematic = true;
    
            // Deshabilitar colisiones
            foreach (var col in _colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
    
            // Cambiar layer si es necesario
            if (_grabbedLayer != -1)
            {
                gameObject.layer = (int)Mathf.Log(_grabbedLayer.value, 2);
            }
    
            // Teleport para limpiar buffer
            if (_networkRigidbody != null)
            {
                _networkRigidbody.Teleport();
            }
    
            // Actualizar estado de red (los offsets YA están guardados)
            IsGrabbed = true;
            CurrentGrabber = Runner.LocalPlayer;
            IsKinematic = true;
    
            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== APPLY GRAB END =====");
            }
        }

        private void ReleaseObject()
        {
            if (_useSnapSlots && TrySnapToSlot())
            {
                IsGrabbed = false;
                CurrentGrabber = PlayerRef.None;
                IsKinematic = true;
                return;
            }

            IsGrabbed = false;
            CurrentGrabber = PlayerRef.None;
            IsKinematic = _wasKinematicBeforeGrab;

            _rigidbody.isKinematic = _wasKinematicBeforeGrab;

            foreach (var col in _colliders)
            {
                if (col != null)
                    col.enabled = true;
            }

            if (_maintainVelocityOnRelease && !_wasKinematicBeforeGrab)
            {
                _rigidbody.linearVelocity = LastVelocity * _throwForceMultiplier;
                _rigidbody.angularVelocity = LastAngularVelocity;

                if (_debugMode)
                    Debug.Log($"[FusionVRGrabbable] Applied throw velocity: {LastVelocity.magnitude:F2}");
            }

            gameObject.layer = _originalLayer;

            if (_debugMode)
                Debug.Log($"[FusionVRGrabbable] Released, colliders re-enabled");
        }

        private void ForceRelease()
        {
            _isLocallyGrabbed = false;
            _localGrabber = null;

            if (HasStateAuthority)
            {
                ReleaseObject();
            }
        }

        #endregion

        #region Movement & Physics

        private void ApplyGrabMovement()
        {
            if (_localGrabber == null) return;

            Vector3 targetPosition = _localGrabber.TransformPoint(GrabOffset);
            Quaternion targetRotation = _localGrabber.rotation * GrabRotationOffset;

            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        private void TrackVelocity()
        {
            // Calcular velocidad basada en cambio de posición
            Vector3 currentVelocity = (transform.position - _velocityTracker) / Runner.DeltaTime;
            LastVelocity = Vector3.Lerp(LastVelocity, currentVelocity, 0.5f);

            // Calcular velocidad angular
            Quaternion deltaRotation =
                transform.rotation * Quaternion.Inverse(Quaternion.Euler(_angularVelocityTracker));
            float angle;
            Vector3 axis;
            deltaRotation.ToAngleAxis(out angle, out axis);
            LastAngularVelocity = axis * (angle * Mathf.Deg2Rad / Runner.DeltaTime);

            // Guardar posición actual para el siguiente frame
            _velocityTracker = transform.position;
            _angularVelocityTracker = transform.rotation.eulerAngles;
        }

        #endregion

        #region Slot System

        private bool TrySnapToSlot()
        {
            // Buscar slots cercanos
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _snapDistance);

            foreach (var collider in nearbyColliders)
            {
                if (collider.CompareTag(_slotTag))
                {
                    Transform slot = collider.transform;

                    // Snap al slot
                    transform.position = slot.position;
                    transform.rotation = slot.rotation;

                    // Hacer kinematic
                    _rigidbody.isKinematic = true;
                    IsKinematic = true;

                    if (_debugMode)
                        Debug.Log($"[FusionVRGrabbable] Snapped to slot: {slot.name}");

                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Network Callbacks

        private void OnIsGrabbedChanged()
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== OnIsGrabbedChanged CALLBACK =====");
            Debug.Log($"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - IsGrabbed: {IsGrabbed}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - CurrentGrabber: {CurrentGrabber}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - _isLocallyGrabbed: {_isLocallyGrabbed}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - HasStateAuthority: {HasStateAuthority}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Object Position: {transform.position}");

            if (!_isLocallyGrabbed) // Solo para clientes remotos
            {
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - REMOTE CLIENT processing grab state change");

                if (IsGrabbed)
                {
                    Debug.Log(
                        $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Remote: Setting kinematic=true, disabling colliders");

                    _rigidbody.isKinematic = true;

                    foreach (var col in _colliders)
                    {
                        if (col != null)
                            col.enabled = false;
                    }
                }
                else
                {
                    Debug.Log(
                        $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Remote: Restoring kinematic={IsKinematic}, enabling colliders");

                    _rigidbody.isKinematic = IsKinematic;

                    foreach (var col in _colliders)
                    {
                        if (col != null)
                            col.enabled = true;
                    }
                }
            }
            else
            {
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - LOCAL CLIENT, skipping remote processing");
            }

            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ===== OnIsGrabbedChanged END =====");
        }

        private void CheckBreakDistance()
        {
            if (_localGrabber == null) return;

            // Calcular distancia entre mano y objeto
            float distance = Vector3.Distance(transform.position, _localGrabber.position);

            // Si excede el umbral, soltar automáticamente
            if (distance > _grabBreakDistance)
            {
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ⚠️ BREAK DISTANCE EXCEEDED!");
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Distance: {distance:F2}m, Threshold: {_grabBreakDistance}m");
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Object pos: {transform.position}, Hand pos: {_localGrabber.position}");

                // Forzar release
                ForceRelease();

                // Opcional: Feedback visual/háptico
                if (_debugMode)
                {
                    Debug.LogWarning(
                        $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Auto-released due to distance break");
                }
            }
        }

        private Transform CreateGrabberProxy(Pose pose)
        {
            if (_grabberProxy == null)
            {
                _grabberProxy = new GameObject($"{name}_GrabProxy");
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Created proxy grabber");
            }

            _grabberProxy.transform.position = pose.position;
            _grabberProxy.transform.rotation = pose.rotation;

            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Proxy position: {pose.position}");

            return _grabberProxy.transform;
        }

        private void OnIsKinematicChanged()
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - OnIsKinematicChanged: {IsKinematic}");

            if (!HasStateAuthority && !_isLocallyGrabbed)
            {
                _rigidbody.isKinematic = IsKinematic;
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Remote: Kinematic synced to {IsKinematic}");
            }
        }

        #endregion

        #region Validation & Debug

        private void ValidateSetup()
        {
            if (_networkRigidbody == null)
            {
                Debug.LogError($"[FusionVRGrabbable] {name} - Missing NetworkRigidbody3D!");
            }

            if (GetComponent<NetworkTransform>() != null)
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] {name} - Has NetworkTransform! Should only use NetworkRigidbody3D");
            }

            if (GetComponent<OneGrabFreeTransformer>() != null ||
                GetComponent<GrabFreeTransformer>() != null ||
                GetComponent<MoveTowardsTargetProvider>() != null)
            {
                Debug.LogWarning($"[FusionVRGrabbable] {name} - Has conflicting transformer components!");
            }

            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && !netObj.Flags.HasFlag(NetworkObjectFlags.AllowStateAuthorityOverride))
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] {name} - NetworkObject.AllowStateAuthorityOverride MUST be TRUE for grab to work!");
            }

            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] {name} - Setup validated");
                Debug.Log(
                    "Make sure NetworkRunner has RunnerSimulatePhysics3D with ClientPhysicsSimulation set appropriately");
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Mostrar estado
            Gizmos.color = IsGrabbed ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            // Mostrar conexión al grabber
            if (_localGrabber != null)
            {
                Gizmos.color = HasStateAuthority ? Color.green : Color.yellow;
                Gizmos.DrawLine(transform.position, _localGrabber.position);
            }

            // Mostrar slots cercanos
            if (_useSnapSlots)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _snapDistance);
            }
        }

        void OnDestroy()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised -= OnMetaGrabbableEvent;
            }

            if (_authorityRequestCoroutine != null)
            {
                StopCoroutine(_authorityRequestCoroutine);
            }
        }

        #endregion
    }
}
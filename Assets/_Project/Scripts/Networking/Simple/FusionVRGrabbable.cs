using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Oculus.Interaction;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Sistema de grab para VR con Fusion 2 usando NetworkRigidbody3D
    /// Arquitectura optimizada con UniTask para operaciones asíncronas
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkRigidbody3D))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public class FusionVRGrabbable : NetworkBehaviour, IStateAuthorityChanged
    {
        #region Serialized Fields

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

        #endregion

        #region Components

        private NetworkRigidbody3D _networkRigidbody;
        private Rigidbody _rigidbody;
        private Grabbable _metaGrabbable;
        private Collider[] _colliders;

        #endregion

        #region Networked State

        [Networked, OnChangedRender(nameof(OnIsGrabbedChanged))]
        public NetworkBool IsGrabbed { get; set; }

        [Networked] public PlayerRef CurrentGrabber { get; set; }
        [Networked] public Vector3 GrabOffset { get; set; }
        [Networked] public Quaternion GrabRotationOffset { get; set; }

        [Networked, OnChangedRender(nameof(OnIsKinematicChanged))]
        public NetworkBool IsKinematic { get; set; }

        [Networked] public Vector3 LastVelocity { get; set; }
        [Networked] public Vector3 LastAngularVelocity { get; set; }

        #endregion

        #region Local State

        private bool _isLocallyGrabbed = false;
        private Transform _localGrabber;
        private Vector3 _velocityTracker;
        private Vector3 _angularVelocityTracker;
        private int _originalLayer;
        private bool _wasKinematicBeforeGrab;
        private Vector3 _tempGrabOffset;
        private Quaternion _tempGrabRotationOffset;

        #endregion

        #region Authority Management

        private CancellationTokenSource _authorityCancellation;
        private GameObject _grabberProxy;
        private Transform _cachedGrabberTransform;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            _rigidbody = GetComponent<Rigidbody>();
            _metaGrabbable = GetComponent<Grabbable>();
            _colliders = GetComponentsInChildren<Collider>();
            _originalLayer = gameObject.layer;

            ValidateSetup();
        }

        void Start()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised += OnMetaGrabbableEvent;
            }
        }

        void OnDestroy()
        {
            if (_metaGrabbable != null)
            {
                _metaGrabbable.WhenPointerEventRaised -= OnMetaGrabbableEvent;
            }

            _authorityCancellation?.Cancel();
            _authorityCancellation?.Dispose();
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

            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkRigidbody == null) _networkRigidbody = GetComponent<NetworkRigidbody3D>();

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
            if (IsGrabbed && Runner != null && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - PERIODIC UPDATE:");
                Debug.Log($"  IsGrabbed: {IsGrabbed}, Grabber: {CurrentGrabber}");
                Debug.Log($"  HasAuthority: {HasStateAuthority}, LocalGrabbed: {_isLocallyGrabbed}");
                Debug.Log($"  Position: {transform.position}");

                if (_localGrabber != null)
                {
                    Debug.Log($"  Grabber Position: {_localGrabber.position}");
                    Debug.Log($"  GrabOffset: {GrabOffset}");
                    Debug.Log($"  GrabRotOffset: {GrabRotationOffset.eulerAngles}");
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

        #region Meta XR Integration

        private void OnMetaGrabbableEvent(PointerEvent evt)
        {
            // Validaciones
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
                    HandleSelectEvent(evt).Forget();
                    break;

                case PointerEventType.Move:
                    HandleMoveEvent(evt);
                    break;

                case PointerEventType.Unselect:
                    HandleUnselectEvent();
                    break;

                case PointerEventType.Unhover:
                    _cachedGrabberTransform = null;
                    Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Unhover, cache cleared");
                    break;
            }
        }

        private async UniTaskVoid HandleSelectEvent(PointerEvent evt)
        {
            if (_isLocallyGrabbed)
            {
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - SELECT ignored, already locally grabbed");
                return;
            }

            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== SELECT START =====");
            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Object Position: {transform.position}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - evt.Pose: pos={evt.Pose.position}, rot={evt.Pose.rotation.eulerAngles}");

            // Buscar grabber
            Transform grabberTransform = _cachedGrabberTransform ?? FindActiveGrabberForThisObject();

            if (grabberTransform == null)
            {
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Real grabber not found, using proxy");
                grabberTransform = CreateGrabberProxy(evt.Pose);
            }

            if (grabberTransform == null)
            {
                Debug.LogError($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Failed to get/create grabber!");
                return;
            }

            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Grabber: {grabberTransform.name}, Position: {grabberTransform.position}");

            // Hacer kinematic inmediatamente
            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Setting kinematic=true and teleporting...");
            _rigidbody.isKinematic = true;
            _networkRigidbody.Teleport();

            _isLocallyGrabbed = true;
            _localGrabber = grabberTransform;

            // ✅ GUARDAR EN VARIABLES LOCALES TEMPORALES (NO en propiedades networked)
            _tempGrabOffset = _localGrabber.InverseTransformPoint(transform.position);
            _tempGrabRotationOffset = Quaternion.Inverse(_localGrabber.rotation) * transform.rotation;

            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - TEMP GrabOffset: {_tempGrabOffset}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - TEMP GrabRotOffset: {_tempGrabRotationOffset.eulerAngles}");
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - TEMP GrabRotOffset RAW: w={_tempGrabRotationOffset.w}, x={_tempGrabRotationOffset.x}, y={_tempGrabRotationOffset.y}, z={_tempGrabRotationOffset.z}");

            if (!HasStateAuthority)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Requesting authority...");
                await RequestAuthorityAsync(2f);
            }
            else
            {
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Already have authority, applying grab immediately");
                ApplyGrab();
            }

            Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== SELECT END =====");
        }

        private void HandleMoveEvent(PointerEvent evt)
        {
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
        }

        private void HandleUnselectEvent()
        {
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

            if (_grabberProxy != null)
            {
                Destroy(_grabberProxy);
                _grabberProxy = null;
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Proxy destroyed");
            }
        }

        #endregion

        #region Authority Management

        private async UniTask RequestAuthorityAsync(float timeout)
        {
            Debug.Log(
                $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Authority request started. Current authority: {Object.StateAuthority}");

            // Cancelar request anterior si existe
            _authorityCancellation?.Cancel();
            _authorityCancellation?.Dispose();
            _authorityCancellation = new CancellationTokenSource();

            Object.RequestStateAuthority();

            try
            {
                // Esperar autoridad con timeout
                await UniTask.WaitUntil(
                    () => HasStateAuthority,
                    cancellationToken: _authorityCancellation.Token
                ).Timeout(System.TimeSpan.FromSeconds(timeout));

                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ✓ Authority acquired. New authority: {Object.StateAuthority}");
                ApplyGrab();
            }
            catch (System.OperationCanceledException)
            {
                Debug.LogWarning($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Authority request cancelled");
                CleanupFailedGrab();
            }
            catch (System.TimeoutException)
            {
                Debug.LogError(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ✗ Authority request TIMEOUT after {timeout}s! Authority still: {Object.StateAuthority}");
                CleanupFailedGrab();
            }
            finally
            {
                _authorityCancellation?.Dispose();
                _authorityCancellation = null;
            }
        }

        private void CleanupFailedGrab()
        {
            _isLocallyGrabbed = false;
            _localGrabber = null;
            _rigidbody.isKinematic = IsKinematic;

            if (_metaGrabbable != null)
            {
                _metaGrabbable.enabled = false;
                _metaGrabbable.enabled = true;
            }
        }

        #endregion

        #region Grab Logic

        private void ApplyGrab()
        {
            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - ===== APPLY GRAB START =====");
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Writing temp offsets to networked properties...");
            }

            _wasKinematicBeforeGrab = _rigidbody.isKinematic;
            _rigidbody.isKinematic = true;

            foreach (var col in _colliders)
            {
                if (col != null)
                    col.enabled = false;
            }

            if (_grabbedLayer != -1)
            {
                gameObject.layer = (int)Mathf.Log(_grabbedLayer.value, 2);
            }

            if (_networkRigidbody != null)
            {
                _networkRigidbody.Teleport();
            }

            // ✅ AHORA SÍ ESCRIBIR EN PROPIEDADES NETWORKED (tenemos autoridad)
            GrabOffset = _tempGrabOffset;
            GrabRotationOffset = _tempGrabRotationOffset;

            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Networked offsets written:");
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} -   GrabOffset: {GrabOffset}");
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} -   GrabRotOffset: {GrabRotationOffset.eulerAngles}");
                Debug.Log(
                    $"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} -   GrabRotOffset RAW: w={GrabRotationOffset.w}, x={GrabRotationOffset.x}, y={GrabRotationOffset.y}, z={GrabRotationOffset.z}");
            }

            IsGrabbed = true;
            CurrentGrabber = Runner.LocalPlayer;
            IsKinematic = true;

            if (_debugMode)
            {
                Debug.Log($"[FusionVRGrabbable] [{Runner.LocalPlayer}] {name} - Network state updated");
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

            // Si es grab local, usar variables temporales hasta que se sincronicen
            Vector3 offsetToUse = HasStateAuthority ? GrabOffset : _tempGrabOffset;
            Quaternion rotOffsetToUse = HasStateAuthority ? GrabRotationOffset : _tempGrabRotationOffset;

            Vector3 targetPosition = _localGrabber.TransformPoint(offsetToUse);
            Quaternion targetRotation = _localGrabber.rotation * rotOffsetToUse;

            if (_debugMode && Time.frameCount % 120 == 0)
            {
                Debug.Log($"[ApplyGrabMovement] [{Runner.LocalPlayer}] {name}:");
                Debug.Log($"  Using offsets: {(HasStateAuthority ? "NETWORKED" : "TEMP")}");
                Debug.Log($"  Offset: {offsetToUse}, RotOffset: {rotOffsetToUse.eulerAngles}");
                Debug.Log($"  Grabber rot: {_localGrabber.rotation.eulerAngles}");
                Debug.Log($"  Target rot: {targetRotation.eulerAngles}");
            }

            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        private void TrackVelocity()
        {
            Vector3 currentVelocity = (transform.position - _velocityTracker) / Runner.DeltaTime;
            LastVelocity = Vector3.Lerp(LastVelocity, currentVelocity, 0.5f);

            Quaternion deltaRotation =
                transform.rotation * Quaternion.Inverse(Quaternion.Euler(_angularVelocityTracker));
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            LastAngularVelocity = axis * (angle * Mathf.Deg2Rad / Runner.DeltaTime);

            _velocityTracker = transform.position;
            _angularVelocityTracker = transform.rotation.eulerAngles;
        }

        private void CheckBreakDistance()
        {
            if (_localGrabber == null) return;

            float distance = Vector3.Distance(transform.position, _localGrabber.position);

            if (distance > _grabBreakDistance)
            {
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ⚠️ BREAK DISTANCE EXCEEDED!");
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Distance: {distance:F2}m, Threshold: {_grabBreakDistance}m");
                Debug.LogWarning(
                    $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Object pos: {transform.position}, Hand pos: {_localGrabber.position}");

                ForceRelease();

                if (_debugMode)
                {
                    Debug.LogWarning(
                        $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - Auto-released due to distance break");
                }
            }
        }

        #endregion

        #region Helper Methods

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
                            $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - ✓ Found grabber: {grabber.transform.name}");
                        return grabber.transform;
                    }
                }
            }

            Debug.LogWarning(
                $"[FusionVRGrabbable] [{Runner?.LocalPlayer ?? PlayerRef.None}] {name} - No matching grabber found");
            return null;
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

        private bool TrySnapToSlot()
        {
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _snapDistance);

            foreach (var collider in nearbyColliders)
            {
                if (collider.CompareTag(_slotTag))
                {
                    Transform slot = collider.transform;
                    transform.position = slot.position;
                    transform.rotation = slot.rotation;
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

            if (!_isLocallyGrabbed)
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

            Gizmos.color = IsGrabbed ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            if (_localGrabber != null)
            {
                Gizmos.color = HasStateAuthority ? Color.green : Color.yellow;
                Gizmos.DrawLine(transform.position, _localGrabber.position);
            }

            if (_useSnapSlots)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _snapDistance);
            }
        }

        #endregion
    }
}
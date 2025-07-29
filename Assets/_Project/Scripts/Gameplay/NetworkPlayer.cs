using UnityEngine;
using Fusion;
using System.Collections;
using HackMonkeys.Core;

namespace HackMonkeys.Gameplay
{
    /// <summary>
    /// NetworkPlayer - Representa un jugador VR sincronizado en la red
    /// Auto-detecta y conecta el VR Rig local existente en la escena
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        #region Network Properties
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public Color PlayerColor { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        
        // Posiciones sincronizadas
        [Networked] public Vector3 HeadPosition { get; set; }
        [Networked] public Quaternion HeadRotation { get; set; }
        [Networked] public Vector3 LeftHandPosition { get; set; }
        [Networked] public Quaternion LeftHandRotation { get; set; }
        [Networked] public Vector3 RightHandPosition { get; set; }
        [Networked] public Quaternion RightHandRotation { get; set; }
        
        // Estados de las manos
        [Networked] public float LeftHandGrip { get; set; }
        [Networked] public float RightHandGrip { get; set; }
        [Networked] public float LeftHandTrigger { get; set; }
        [Networked] public float RightHandTrigger { get; set; }
        #endregion

        #region Components References
        [Header("Avatar Components")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Animator avatarAnimator;
        
        [Header("Visual Components")]
        [SerializeField] private SkinnedMeshRenderer avatarRenderer;
        [SerializeField] private GameObject nameTagPrefab;
        [SerializeField] private float nameTagHeight = 2.2f;
        
        [Header("Hand Models")]
        [SerializeField] private GameObject leftHandModel;
        [SerializeField] private GameObject rightHandModel;
        [SerializeField] private LineRenderer leftHandRay;
        [SerializeField] private LineRenderer rightHandRay;
        
        [Header("Audio")]
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private float voiceMaxDistance = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Private Fields
        private GameObject _localVRRig;
        private Transform _vrCameraTransform;
        private Transform _vrLeftHandTransform;
        private Transform _vrRightHandTransform;
        private TMPro.TextMeshPro _nameTagText;
        private bool _isLocalPlayer;
        private bool _hasCheckedAuthority = false;
        private PlayerDataManager _playerDataManager;
        
        // Interpolación
        private float _interpolationTime = 0.1f;
        private bool _vrRigConnected = false;
        
        // Debug
        private int _frameCounter = 0;
        #endregion

        #region Initialization
        public override void Spawned()
        {
            Debug.Log($"[NetworkPlayer] 🎮 Player spawned: {Object.InputAuthority}");
            
            // FIX: NO usar HasInputAuthority aquí - puede no estar listo
            // Usar comparación directa en su lugar
            _isLocalPlayer = Object.InputAuthority == Runner.LocalPlayer;
            
            Debug.Log($"[NetworkPlayer] Authority Check:");
            Debug.Log($"  - Object.InputAuthority: {Object.InputAuthority}");
            Debug.Log($"  - Runner.LocalPlayer: {Runner.LocalPlayer}");
            Debug.Log($"  - _isLocalPlayer: {_isLocalPlayer}");
            Debug.Log($"  - HasInputAuthority: {HasInputAuthority} (may be unreliable during Spawned)");
            
            _playerDataManager = PlayerDataManager.Instance;
            
            if (_isLocalPlayer)
            {
                SetupLocalPlayer();
                StartCoroutine(AutoConnectVRRig());
            }
            else
            {
                SetupRemotePlayer();
            }
            
            StartCoroutine(InitializePlayerData());
        }

        private IEnumerator InitializePlayerData()
        {
            yield return new WaitForSeconds(0.1f);
            
            if (_isLocalPlayer && _playerDataManager != null)
            {
                RPC_SetPlayerData(
                    _playerDataManager.GetPlayerName(),
                    _playerDataManager.GetPlayerColor()
                );
            }
            
            CreateNameTag();
            ApplyPlayerColor();
        }

        private void SetupLocalPlayer()
        {
            Debug.Log("[NetworkPlayer] 🥽 Setting up local VR player");
            
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("LocalPlayer"));
            
            if (avatarRenderer != null)
            {
                avatarRenderer.enabled = false;
            }
            
            if (voiceAudioSource != null)
            {
                voiceAudioSource.spatialBlend = 0f;
            }
            
            Debug.Log("[NetworkPlayer] 📍 Esperando conexión del VR Rig...");
        }

        private void SetupRemotePlayer()
        {
            Debug.Log($"[NetworkPlayer] 👤 Setting up remote player: {Object.InputAuthority}");
            
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("RemotePlayer"));
            
            if (voiceAudioSource != null)
            {
                voiceAudioSource.spatialBlend = 1f;
                voiceAudioSource.maxDistance = voiceMaxDistance;
                voiceAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }
            
            StartCoroutine(InterpolateMovement());
        }

        private IEnumerator AutoConnectVRRig()
        {
            Debug.Log("[NetworkPlayer] 🔍 Buscando VR Rig local...");
            
            // Esperar un frame para asegurar inicialización
            yield return null;
            
            GameObject vrRig = null;
            
            // Buscar por tag primero
            if (GameObject.FindGameObjectWithTag("LocalVRRig") != null)
            {
                vrRig = GameObject.FindGameObjectWithTag("LocalVRRig");
                Debug.Log("[NetworkPlayer] ✅ VR Rig encontrado por tag");
            }
            // Si no, buscar por componente
            else
            {
                var ovrCameraRig = FindObjectOfType<OVRCameraRig>();
                if (ovrCameraRig != null)
                {
                    vrRig = ovrCameraRig.gameObject;
                    Debug.Log("[NetworkPlayer] ✅ VR Rig encontrado por componente");
                }
            }
            
            if (vrRig != null)
            {
                SetVRRig(vrRig);
                
                // Notificar a GameplayManager
                var gameplayManager = GameplayManager.Instance;
                if (gameplayManager != null)
                {
                    gameplayManager.RegisterLocalPlayer(this);
                }
            }
            else
            {
                Debug.LogError("[NetworkPlayer] ❌ No se encontró VR Rig en la escena!");
            }
        }
        #endregion

        #region VR Rig Connection
        public void SetVRRig(GameObject vrRig)
        {
            // FIX: Verificar usando la comparación directa
            if (Object.InputAuthority != Runner.LocalPlayer)
            {
                Debug.LogWarning("[NetworkPlayer] ❌ SetVRRig llamado en jugador remoto!");
                return;
            }
            
            if (_vrRigConnected)
            {
                Debug.LogWarning("[NetworkPlayer] ⚠️ VR Rig ya estaba conectado!");
                return;
            }
            
            _localVRRig = vrRig;
            
            OVRCameraRig cameraRig = vrRig.GetComponent<OVRCameraRig>();
            if (cameraRig != null)
            {
                _vrCameraTransform = cameraRig.centerEyeAnchor;
                _vrLeftHandTransform = cameraRig.leftHandAnchor;
                _vrRightHandTransform = cameraRig.rightHandAnchor;
                
                _vrRigConnected = true;
                
                Debug.Log("[NetworkPlayer] ✅ VR Rig conectado exitosamente");
                Debug.Log($"  - Camera: {_vrCameraTransform != null}");
                Debug.Log($"  - Left Hand: {_vrLeftHandTransform != null}");
                Debug.Log($"  - Right Hand: {_vrRightHandTransform != null}");
            }
            else
            {
                Debug.LogError("[NetworkPlayer] ❌ OVRCameraRig no encontrado en el VR Rig!");
            }
        }
        #endregion

        #region Network Update
        public override void FixedUpdateNetwork()
        {
            // FIX: Verificación adicional de autoridad después de algunos ticks
            if (!_hasCheckedAuthority && Runner.Tick > 10)
            {
                bool shouldBeLocal = Object.InputAuthority == Runner.LocalPlayer;
                if (shouldBeLocal != _isLocalPlayer)
                {
                    Debug.LogWarning($"[NetworkPlayer] Authority mismatch detected! Correcting...");
                    Debug.Log($"  - Was local: {_isLocalPlayer}");
                    Debug.Log($"  - Should be local: {shouldBeLocal}");
                    
                    _isLocalPlayer = shouldBeLocal;
                    
                    if (_isLocalPlayer && !_vrRigConnected)
                    {
                        // Re-intentar configuración local
                        SetupLocalPlayer();
                        StartCoroutine(AutoConnectVRRig());
                    }
                }
                _hasCheckedAuthority = true;
            }
            
            // FIX: Usar comparación directa en lugar de HasInputAuthority
            if (Object.InputAuthority != Runner.LocalPlayer)
            {
                return;
            }
            
            // Verificar que el VR esté conectado
            if (!_vrRigConnected || _localVRRig == null)
            {
                if (enableDebugLogs && Runner.Tick % 60 == 0) // Log cada segundo
                {
                    Debug.LogWarning($"[NetworkPlayer-{Runner.LocalPlayer}] VR no conectado en FixedUpdateNetwork");
                }
                return;
            }
            
            // Sincronizar posiciones
            SyncVRPositions();
            
            // Debug cada segundo
            if (enableDebugLogs && Runner.Tick % 60 == 0)
            {
                Debug.Log($"[NetworkPlayer-{Runner.LocalPlayer}] Sync - Head: {HeadPosition}");
            }
        }

        private void SyncVRPositions()
        {
            // Sincronizar cabeza
            if (_vrCameraTransform != null)
            {
                Vector3 oldPos = HeadPosition;
                HeadPosition = _vrCameraTransform.position;
                HeadRotation = _vrCameraTransform.rotation;
                
                // Debug si hay movimiento significativo
                if (enableDebugLogs && Vector3.Distance(oldPos, HeadPosition) > 0.1f)
                {
                    Debug.Log($"[CLIENT-{Runner.LocalPlayer}] Head moved: {oldPos} -> {HeadPosition}");
                }
            }
            
            // Sincronizar manos
            if (_vrLeftHandTransform != null)
            {
                LeftHandPosition = _vrLeftHandTransform.position;
                LeftHandRotation = _vrLeftHandTransform.rotation;
            }
            
            if (_vrRightHandTransform != null)
            {
                RightHandPosition = _vrRightHandTransform.position;
                RightHandRotation = _vrRightHandTransform.rotation;
            }
            
            // Sincronizar inputs
            LeftHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
            RightHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            LeftHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            RightHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        }

        private void Update()
        {
            _frameCounter++;
            
            // FIX: Usar comparación directa también en Update
            bool isActuallyLocal = Object.InputAuthority == Runner.LocalPlayer;
            
            if (isActuallyLocal)
            {
                UpdateLocalPlayer();
            }
            else
            {
                UpdateRemotePlayer();
                
                // Debug para el HOST
                if (enableDebugLogs && _frameCounter % 120 == 0) // Cada 2 segundos
                {
                    Debug.Log($"[HOST] Remote player {Object.InputAuthority} at: {HeadPosition}");
                }
            }
            
            UpdateNameTagOrientation();
        }

        private void UpdateLocalPlayer()
        {
            if (!_vrRigConnected || _localVRRig == null) return;
            
            // El NetworkPlayer sigue al VR Rig
            transform.position = _localVRRig.transform.position;
            
            // Rotación basada en la cabeza
            if (_vrCameraTransform != null)
            {
                Vector3 forward = _vrCameraTransform.forward;
                forward.y = 0;
                if (forward.magnitude > 0.1f)
                {
                    transform.rotation = Quaternion.LookRotation(forward);
                }
            }
        }

        private void UpdateRemotePlayer()
        {
            // Actualizar componentes visuales con las posiciones sincronizadas
            if (headTransform != null)
            {
                headTransform.position = HeadPosition;
                headTransform.rotation = HeadRotation;
            }
            
            if (leftHandTransform != null)
            {
                leftHandTransform.position = LeftHandPosition;
                leftHandTransform.rotation = LeftHandRotation;
                
                if (avatarAnimator != null)
                {
                    avatarAnimator.SetFloat("LeftGrip", LeftHandGrip);
                }
            }
            
            if (rightHandTransform != null)
            {
                rightHandTransform.position = RightHandPosition;
                rightHandTransform.rotation = RightHandRotation;
                
                if (avatarAnimator != null)
                {
                    avatarAnimator.SetFloat("RightGrip", RightHandGrip);
                }
            }
            
            // Actualizar posición del cuerpo
            Vector3 bodyPosition = HeadPosition;
            bodyPosition.y = transform.position.y;
            transform.position = bodyPosition;
            
            // Rotación del cuerpo
            Vector3 headForward = HeadRotation * Vector3.forward;
            headForward.y = 0;
            if (headForward.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(headForward);
            }
        }
        #endregion

        #region Interpolation
        private IEnumerator InterpolateMovement()
        {
            while (Object.InputAuthority != Runner.LocalPlayer) // FIX: Usar comparación directa
            {
                if (headTransform != null)
                {
                    headTransform.position = Vector3.Lerp(
                        headTransform.position, 
                        HeadPosition, 
                        Time.deltaTime / _interpolationTime
                    );
                    
                    headTransform.rotation = Quaternion.Slerp(
                        headTransform.rotation,
                        HeadRotation,
                        Time.deltaTime / _interpolationTime
                    );
                }
                
                // Interpolar manos también
                if (leftHandTransform != null)
                {
                    leftHandTransform.position = Vector3.Lerp(
                        leftHandTransform.position,
                        LeftHandPosition,
                        Time.deltaTime / _interpolationTime
                    );
                    
                    leftHandTransform.rotation = Quaternion.Slerp(
                        leftHandTransform.rotation,
                        LeftHandRotation,
                        Time.deltaTime / _interpolationTime
                    );
                }
                
                if (rightHandTransform != null)
                {
                    rightHandTransform.position = Vector3.Lerp(
                        rightHandTransform.position,
                        RightHandPosition,
                        Time.deltaTime / _interpolationTime
                    );
                    
                    rightHandTransform.rotation = Quaternion.Slerp(
                        rightHandTransform.rotation,
                        RightHandRotation,
                        Time.deltaTime / _interpolationTime
                    );
                }
                
                yield return null;
            }
        }
        #endregion

        #region Visual Setup
        private void CreateNameTag()
        {
            if (nameTagPrefab == null) return;
            
            GameObject nameTag = Instantiate(nameTagPrefab, transform);
            nameTag.transform.localPosition = new Vector3(0, nameTagHeight, 0);
            
            _nameTagText = nameTag.GetComponentInChildren<TMPro.TextMeshPro>();
            if (_nameTagText != null)
            {
                _nameTagText.text = PlayerName.ToString();
                
                // FIX: Usar comparación directa
                if (Object.InputAuthority == Runner.LocalPlayer)
                {
                    nameTag.SetActive(false);
                }
            }
        }

        private void UpdateNameTagOrientation()
        {
            if (_nameTagText == null || Camera.main == null) return;
            
            Vector3 lookDirection = Camera.main.transform.position - _nameTagText.transform.position;
            lookDirection.y = 0;
            
            if (lookDirection.magnitude > 0.1f)
            {
                _nameTagText.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        private void ApplyPlayerColor()
        {
            if (avatarRenderer != null)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetColor("_Color", PlayerColor);
                avatarRenderer.SetPropertyBlock(props);
            }
            
            ApplyColorToHands();
        }

        private void ApplyColorToHands()
        {
            if (leftHandModel != null)
            {
                Renderer leftRenderer = leftHandModel.GetComponent<Renderer>();
                if (leftRenderer != null)
                {
                    leftRenderer.material.color = PlayerColor;
                }
            }
            
            if (rightHandModel != null)
            {
                Renderer rightRenderer = rightHandModel.GetComponent<Renderer>();
                if (rightRenderer != null)
                {
                    rightRenderer.material.color = PlayerColor;
                }
            }
        }
        #endregion

        #region RPCs
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color)
        {
            PlayerName = name;
            PlayerColor = color;
            
            Debug.Log($"[NetworkPlayer] Player data set: {name}");
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }
        #endregion

        #region Public Methods
        public void EnableControls(bool enabled)
        {
            // FIX: Usar comparación directa
            if (Object.InputAuthority != Runner.LocalPlayer || _localVRRig == null) return;
            
            var locomotion = _localVRRig.GetComponentInChildren<OVRPlayerController>();
            if (locomotion != null)
            {
                locomotion.enabled = enabled;
            }
        }

        public void TeleportTo(Vector3 position)
        {
            // FIX: Usar comparación directa
            if (Object.InputAuthority != Runner.LocalPlayer || _localVRRig == null) return;
            
            _localVRRig.transform.position = position;
            transform.position = position;
        }

        public bool IsVRRigConnected()
        {
            return _vrRigConnected;
        }
        
        // FIX: Nuevo método para verificar si somos el jugador local
        public bool IsLocalPlayer()
        {
            return Object.InputAuthority == Runner.LocalPlayer;
        }
        #endregion

        #region Utilities
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        #endregion

        #region Debug
        private void OnDrawGizmos()
        {
            bool isLocal = Object != null && Object.InputAuthority == Runner?.LocalPlayer;
            Gizmos.color = isLocal ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward);
            
            if (_vrRigConnected)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2);
            }
        }
        
        [ContextMenu("Debug: VR Rig Status")]
        private void DebugVRRigStatus()
        {
            Debug.Log("=== NetworkPlayer VR Rig Status ===");
            Debug.Log($"Player: {Object.InputAuthority}");
            Debug.Log($"Is Local Player (cached): {_isLocalPlayer}");
            Debug.Log($"Is Local Player (actual): {Object.InputAuthority == Runner.LocalPlayer}");
            Debug.Log($"Has Input Authority: {HasInputAuthority}");
            Debug.Log($"VR Rig Connected: {_vrRigConnected}");
            Debug.Log($"Local VR Rig: {_localVRRig}");
            Debug.Log($"Camera Transform: {_vrCameraTransform}");
            Debug.Log($"Left Hand: {_vrLeftHandTransform}");
            Debug.Log($"Right Hand: {_vrRightHandTransform}");
            Debug.Log($"Current Head Pos: {HeadPosition}");
            Debug.Log("================================");
        }
        
        [ContextMenu("Debug: Force Authority Check")]
        private void DebugForceAuthorityCheck()
        {
            bool shouldBeLocal = Object.InputAuthority == Runner.LocalPlayer;
            Debug.Log($"[DEBUG] Force Authority Check:");
            Debug.Log($"  - Current _isLocalPlayer: {_isLocalPlayer}");
            Debug.Log($"  - Should be local: {shouldBeLocal}");
            Debug.Log($"  - Object.InputAuthority: {Object.InputAuthority}");
            Debug.Log($"  - Runner.LocalPlayer: {Runner.LocalPlayer}");
            Debug.Log($"  - HasInputAuthority: {HasInputAuthority}");
            
            if (shouldBeLocal != _isLocalPlayer)
            {
                Debug.Log("[DEBUG] Mismatch detected! Correcting...");
                _isLocalPlayer = shouldBeLocal;
                
                if (_isLocalPlayer && !_vrRigConnected)
                {
                    SetupLocalPlayer();
                    StartCoroutine(AutoConnectVRRig());
                }
            }
        }
        
        [ContextMenu("Debug: Force Sync Test")]
        private void DebugForceSyncTest()
        {
            if (Object.InputAuthority != Runner.LocalPlayer || !_vrRigConnected) return;
            
            Debug.Log("[DEBUG] Forcing sync test...");
            SyncVRPositions();
            Debug.Log($"[DEBUG] Head position after sync: {HeadPosition}");
        }
        #endregion
    }
}
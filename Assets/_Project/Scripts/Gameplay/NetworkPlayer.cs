using System;
using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Fusion.Sockets;
using HackMonkeys.Core;

namespace HackMonkeys.Gameplay
{
    /// <summary>
    /// NetworkPlayer - Representa un jugador VR sincronizado en la red
    /// Usa NetworkInput para sincronización correcta Cliente->Servidor->Clientes
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour, INetworkRunnerCallbacks
    {
        #region Network Properties
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public Color PlayerColor { get; set; }
        [Networked] public NetworkBool IsReady { get; set; }
        
        // Posiciones sincronizadas - Ahora actualizadas por el HOST basándose en Input
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
        
        // Input local
        private NetworkPlayerInput _localInput;
        
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
            
            // Determinar si somos el jugador local
            _isLocalPlayer = Object.InputAuthority == Runner.LocalPlayer;
            
            Debug.Log($"[NetworkPlayer] Authority Check:");
            Debug.Log($"  - Object.InputAuthority: {Object.InputAuthority}");
            Debug.Log($"  - Runner.LocalPlayer: {Runner.LocalPlayer}");
            Debug.Log($"  - _isLocalPlayer: {_isLocalPlayer}");
            
            _playerDataManager = PlayerDataManager.Instance;
            
            // Registrar callbacks para input
            if (Runner != null)
            {
                Runner.AddCallbacks(this);
            }
            
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

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Limpiar callbacks
            if (runner != null)
            {
                runner.RemoveCallbacks(this);
            }
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
            
            yield return null;
            
            GameObject vrRig = null;
            
            if (GameObject.FindGameObjectWithTag("LocalVRRig") != null)
            {
                vrRig = GameObject.FindGameObjectWithTag("LocalVRRig");
                Debug.Log("[NetworkPlayer] ✅ VR Rig encontrado por tag");
            }
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

        #region Network Input System
        private void Update()
        {
            _frameCounter++;
            
            bool isActuallyLocal = Object.InputAuthority == Runner.LocalPlayer;
            
            if (isActuallyLocal)
            {
                // Capturar input local para enviar al servidor
                CaptureLocalInput();
                UpdateLocalPlayer();
            }
            else
            {
                UpdateRemotePlayer();
                
                if (enableDebugLogs && _frameCounter % 120 == 0)
                {
                    Debug.Log($"[REMOTE] Player {Object.InputAuthority} - Head: {HeadPosition}");
                }
            }
            
            UpdateNameTagOrientation();
        }

        private void CaptureLocalInput()
        {
            if (!_vrRigConnected || _localVRRig == null)
            {
                _localInput.isVRConnected = false;
                return;
            }
            
            _localInput.isVRConnected = true;
            
            // Capturar posiciones VR
            if (_vrCameraTransform != null)
            {
                _localInput.headPosition = _vrCameraTransform.position;
                _localInput.headRotation = _vrCameraTransform.rotation;
            }
            
            if (_vrLeftHandTransform != null)
            {
                _localInput.leftHandPosition = _vrLeftHandTransform.position;
                _localInput.leftHandRotation = _vrLeftHandTransform.rotation;
            }
            
            if (_vrRightHandTransform != null)
            {
                _localInput.rightHandPosition = _vrRightHandTransform.position;
                _localInput.rightHandRotation = _vrRightHandTransform.rotation;
            }
            
            // Capturar inputs de botones
            _localInput.leftHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
            _localInput.rightHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            _localInput.leftHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            _localInput.rightHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            
            // Debug
            if (enableDebugLogs && _frameCounter % 60 == 0)
            {
                Debug.Log($"[LOCAL INPUT] Captured - Head: {_localInput.headPosition}");
            }
        }

        // INetworkRunnerCallbacks - OnInput
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // Solo enviar input si somos el jugador local de este NetworkPlayer
            if (Object.InputAuthority == Runner.LocalPlayer)
            {
                input.Set(_localInput);
                
                if (enableDebugLogs && Runner.Tick % 60 == 0)
                {
                    Debug.Log($"[NetworkPlayer] Sending input to server - Head: {_localInput.headPosition}");
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Verificación de autoridad tardía
            if (!_hasCheckedAuthority && Runner.Tick > 10)
            {
                bool shouldBeLocal = Object.InputAuthority == Runner.LocalPlayer;
                if (shouldBeLocal != _isLocalPlayer)
                {
                    Debug.LogWarning($"[NetworkPlayer] Authority mismatch corrected!");
                    _isLocalPlayer = shouldBeLocal;
                    
                    if (_isLocalPlayer && !_vrRigConnected)
                    {
                        SetupLocalPlayer();
                        StartCoroutine(AutoConnectVRRig());
                    }
                }
                _hasCheckedAuthority = true;
            }
            
            // Procesar input (HOST procesa todos, CLIENTE solo el suyo con predicción)
            if (GetInput(out NetworkPlayerInput data))
            {
                // Debug input recibido
                if (enableDebugLogs && Runner.Tick % 60 == 0)
                {
                    Debug.Log($"[NetworkPlayer-{Object.InputAuthority}] Got input in FixedUpdate:");
                    Debug.Log($"  - HasStateAuthority: {HasStateAuthority}");
                    Debug.Log($"  - HasInputAuthority: {HasInputAuthority}");
                    Debug.Log($"  - Input Head Pos: {data.headPosition}");
                    Debug.Log($"  - Input VR Connected: {data.isVRConnected}");
                }
                
                // Solo si el VR está conectado en el input
                if (data.isVRConnected)
                {
                    // El HOST escribe para todos, el CLIENTE predice localmente
                    if (HasStateAuthority || HasInputAuthority)
                    {
                        // Aplicar el input a las propiedades networked
                        HeadPosition = data.headPosition;
                        HeadRotation = data.headRotation;
                        LeftHandPosition = data.leftHandPosition;
                        LeftHandRotation = data.leftHandRotation;
                        RightHandPosition = data.rightHandPosition;
                        RightHandRotation = data.rightHandRotation;
                        LeftHandGrip = data.leftHandGrip;
                        RightHandGrip = data.rightHandGrip;
                        LeftHandTrigger = data.leftHandTrigger;
                        RightHandTrigger = data.rightHandTrigger;
                        
                        if (enableDebugLogs && Runner.Tick % 60 == 0)
                        {
                            Debug.Log($"[NetworkPlayer-{Object.InputAuthority}] Applied input - Head now at: {HeadPosition}");
                        }
                    }
                }
            }
        }
        #endregion

        #region Player Update Methods
        private void UpdateLocalPlayer()
        {
            if (!_vrRigConnected || _localVRRig == null) return;
            
            // El NetworkPlayer sigue al VR Rig localmente
            transform.position = _localVRRig.transform.position;
            
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
            while (Object.InputAuthority != Runner.LocalPlayer)
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
            if (Object.InputAuthority != Runner.LocalPlayer || _localVRRig == null) return;
            
            var locomotion = _localVRRig.GetComponentInChildren<OVRPlayerController>();
            if (locomotion != null)
            {
                locomotion.enabled = enabled;
            }
        }

        public void TeleportTo(Vector3 position)
        {
            if (Object.InputAuthority != Runner.LocalPlayer || _localVRRig == null) return;
            
            _localVRRig.transform.position = position;
            transform.position = position;
        }

        public bool IsVRRigConnected()
        {
            return _vrRigConnected;
        }
        
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

        #region INetworkRunnerCallbacks (Unused)
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
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
            Debug.Log($"Is Local Player: {_isLocalPlayer}");
            Debug.Log($"Has Input Authority: {HasInputAuthority}");
            Debug.Log($"Has State Authority: {HasStateAuthority}");
            Debug.Log($"VR Rig Connected: {_vrRigConnected}");
            Debug.Log($"Local VR Rig: {_localVRRig}");
            Debug.Log($"Current Head Pos: {HeadPosition}");
            Debug.Log($"Local Input Head Pos: {_localInput.headPosition}");
            Debug.Log("================================");
        }
        #endregion
    }
}
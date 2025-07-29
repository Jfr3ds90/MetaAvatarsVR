using UnityEngine;
using Fusion;
using System.Collections;
using HackMonkeys.Core;

namespace HackMonkeys.Gameplay
{
    /// <summary>
    /// NetworkPlayer - Representa un jugador VR sincronizado en la red
    /// Maneja la sincronización de posición, rotación de cabeza y manos
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
        #endregion

        #region Private Fields
        private GameObject _localVRRig;
        private Transform _vrCameraTransform;
        private Transform _vrLeftHandTransform;
        private Transform _vrRightHandTransform;
        private TMPro.TextMeshPro _nameTagText;
        private bool _isLocalPlayer;
        private GameplayManager _gameplayManager;
        private PlayerDataManager _playerDataManager;
        
        // Interpolación
        private Vector3 _previousHeadPosition;
        private Quaternion _previousHeadRotation;
        private float _interpolationTime = 0.1f;
        #endregion

        #region Initialization
        public override void Spawned()
        {
            Debug.Log($"[NetworkPlayer] 🎮 Player spawned: {Object.InputAuthority}");
            
            _isLocalPlayer = HasInputAuthority;
            _gameplayManager = GameplayManager.Instance;
            _playerDataManager = PlayerDataManager.Instance;
            
            if (_isLocalPlayer)
            {
                SetupLocalPlayer();
            }
            else
            {
                SetupRemotePlayer();
            }
            
            // Configurar nombre y color
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
            
            // Crear name tag
            CreateNameTag();
            
            // Aplicar color al avatar
            ApplyPlayerColor();
        }

        private void SetupLocalPlayer()
        {
            Debug.Log("[NetworkPlayer] 🥽 Setting up local VR player");
            
            // Notificar al GameplayManager
            if (_gameplayManager != null)
            {
                _gameplayManager.OnLocalPlayerSpawned(this);
            }
            
            // Configurar layer para evitar auto-renderizado
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("LocalPlayer"));
            
            // Desactivar el renderer del avatar local (solo queremos ver nuestras manos)
            if (avatarRenderer != null)
            {
                avatarRenderer.enabled = false;
            }
            
            // Configurar audio espacial
            if (voiceAudioSource != null)
            {
                voiceAudioSource.spatialBlend = 0f; // 2D para el jugador local
            }
        }

        private void SetupRemotePlayer()
        {
            Debug.Log($"[NetworkPlayer] 👤 Setting up remote player");
            
            // Configurar layer
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("RemotePlayer"));
            
            // Configurar audio espacial
            if (voiceAudioSource != null)
            {
                voiceAudioSource.spatialBlend = 1f; // 3D para jugadores remotos
                voiceAudioSource.maxDistance = voiceMaxDistance;
                voiceAudioSource.rolloffMode = AudioRolloffMode.Linear;
            }
            
            // Habilitar interpolación suave
            StartCoroutine(InterpolateMovement());
        }
        #endregion

        #region VR Rig Connection
        /// <summary>
        /// Conectar el OVRCameraRig local a este NetworkPlayer
        /// </summary>
        public void SetVRRig(GameObject vrRig)
        {
            if (!_isLocalPlayer) return;
            
            _localVRRig = vrRig;
            
            // Encontrar referencias en el VR Rig
            OVRCameraRig cameraRig = vrRig.GetComponent<OVRCameraRig>();
            if (cameraRig != null)
            {
                _vrCameraTransform = cameraRig.centerEyeAnchor;
                _vrLeftHandTransform = cameraRig.leftHandAnchor;
                _vrRightHandTransform = cameraRig.rightHandAnchor;
                
                Debug.Log("[NetworkPlayer] ✅ VR Rig connected");
            }
            else
            {
                Debug.LogError("[NetworkPlayer] ❌ OVRCameraRig not found in VR Rig!");
            }
        }
        #endregion

        #region Network Update
        public override void FixedUpdateNetwork()
        {
            if (!_isLocalPlayer || _localVRRig == null) return;
            
            // Sincronizar posiciones del VR Rig
            if (_vrCameraTransform != null)
            {
                HeadPosition = _vrCameraTransform.position;
                HeadRotation = _vrCameraTransform.rotation;
            }
            
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
            
            // Sincronizar inputs de las manos
            LeftHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
            RightHandGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            LeftHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            RightHandTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        }

        private void Update()
        {
            if (_isLocalPlayer)
            {
                UpdateLocalPlayer();
            }
            else
            {
                UpdateRemotePlayer();
            }
            
            // Actualizar name tag para que mire a la cámara
            UpdateNameTagOrientation();
        }

        private void UpdateLocalPlayer()
        {
            if (_localVRRig == null) return;
            
            // El NetworkPlayer sigue al VR Rig
            transform.position = _localVRRig.transform.position;
            
            // Rotación solo en Y basada en la cabeza
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
            // Los jugadores remotos usan las posiciones sincronizadas
            if (headTransform != null)
            {
                headTransform.position = HeadPosition;
                headTransform.rotation = HeadRotation;
            }
            
            if (leftHandTransform != null)
            {
                leftHandTransform.position = LeftHandPosition;
                leftHandTransform.rotation = LeftHandRotation;
                
                // Animar mano basado en grip
                if (avatarAnimator != null)
                {
                    avatarAnimator.SetFloat("LeftGrip", LeftHandGrip);
                }
            }
            
            if (rightHandTransform != null)
            {
                rightHandTransform.position = RightHandPosition;
                rightHandTransform.rotation = RightHandRotation;
                
                // Animar mano basado en grip
                if (avatarAnimator != null)
                {
                    avatarAnimator.SetFloat("RightGrip", RightHandGrip);
                }
            }
            
            // Actualizar posición del cuerpo para seguir la cabeza
            Vector3 bodyPosition = HeadPosition;
            bodyPosition.y = transform.position.y; // Mantener altura del suelo
            transform.position = bodyPosition;
            
            // Rotación del cuerpo basada en la cabeza
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
            while (!_isLocalPlayer)
            {
                // Interpolar suavemente las posiciones para jugadores remotos
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
                
                // No mostrar nuestro propio nombre
                if (_isLocalPlayer)
                {
                    nameTag.SetActive(false);
                }
            }
        }

        private void UpdateNameTagOrientation()
        {
            if (_nameTagText == null || Camera.main == null) return;
            
            // Hacer que el name tag mire a la cámara
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
                // Aplicar color al material del avatar
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetColor("_Color", PlayerColor);
                avatarRenderer.SetPropertyBlock(props);
            }
            
            // Aplicar color a las manos
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
        /// <summary>
        /// Habilitar/deshabilitar controles del jugador
        /// </summary>
        public void EnableControls(bool enabled)
        {
            if (!_isLocalPlayer || _localVRRig == null) return;
            
            // Habilitar/deshabilitar locomotion
            var locomotion = _localVRRig.GetComponentInChildren<OVRPlayerController>();
            if (locomotion != null)
            {
                locomotion.enabled = enabled;
            }
            
            // Habilitar/deshabilitar interacción
            // TODO: Implementar según tu sistema de interacción
        }

        /// <summary>
        /// Teletransportar jugador
        /// </summary>
        public void TeleportTo(Vector3 position)
        {
            if (!_isLocalPlayer || _localVRRig == null) return;
            
            _localVRRig.transform.position = position;
            transform.position = position;
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
            // Dibujar posición de spawn
            Gizmos.color = _isLocalPlayer ? Color.green : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // Dibujar dirección
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward);
        }
        #endregion
    }
}
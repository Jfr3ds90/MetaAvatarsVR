using UnityEngine;
using Fusion;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Sistema networkeado de linterna VR para puzzle de luz UV
    /// Gestiona estados de luz blanca/UV sincronizados entre jugadores
    /// </summary>
    [RequireComponent(typeof(FusionVRGrabbable))]
    public class NetworkedFlashlight : NetworkBehaviour
    {
        #region Enums
        public enum FlashlightMode
        {
            Off = 0,
            WhiteLight = 1,
            UVLight = 2
        }
        #endregion

        #region Serialized Fields
        [Header("Light Configuration")]
        [SerializeField] private Light spotLight;
        [SerializeField] private float lightRange = 10f;
        [SerializeField] private float lightAngle = 30f;
        [SerializeField] private float lightIntensity = 2f;

        [Header("Light Colors")]
        [SerializeField] private Color whiteLightColor = Color.white;
        [SerializeField] private Color uvLightColor = new Color(0.5f, 0f, 1f); // Purple for UV

        [Header("UV Configuration")]
        [SerializeField] private uint uvRenderingLayer = 1; // Light Layer for UV reveal
        [SerializeField] private uint normalRenderingLayer = 0; // Default light layer

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip switchSound;
        [SerializeField] private AudioClip grabSound;
        [SerializeField] private AudioClip releaseSound;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer flashlightBodyRenderer;
        [SerializeField] private Material whiteLightMaterial;
        [SerializeField] private Material uvLightMaterial;
        [SerializeField] private GameObject uvIndicator; // Visual indicator for UV mode

        #endregion

        #region Networked Properties
        [Networked, OnChangedRender(nameof(OnFlashlightModeChanged))]
        public FlashlightMode CurrentMode { get; set; }

        [Networked, OnChangedRender(nameof(OnIsActiveChanged))]
        public NetworkBool IsActive { get; set; }

        [Networked]
        public PlayerRef CurrentHolder { get; set; }

        // Para sincronizar el estado del botón
        [Networked]
        public NetworkBool ButtonPressed { get; set; }

        // Sincronizar el rendering layer actual
        [Networked, OnChangedRender(nameof(OnRenderingLayerChanged))]
        public uint CurrentRenderingLayer { get; set; }
        #endregion

        #region Private Fields
        private FusionVRGrabbable grabbable;
        private FlashlightButton flashlightButton;
        private NetworkedSpotLight networkedSpotLight; // Referencia al componente NetworkedSpotLight si existe
        private bool isInitialized = false;
        private FlashlightMode lastMode = FlashlightMode.Off;
        private Dictionary<PlayerRef, string> playerNames = new Dictionary<PlayerRef, string>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            grabbable = GetComponent<FusionVRGrabbable>();
            flashlightButton = GetComponentInChildren<FlashlightButton>();

            // Buscar NetworkedSpotLight en el hijo con la luz
            if (spotLight != null)
            {
                networkedSpotLight = spotLight.GetComponent<NetworkedSpotLight>();
            }

            // Si no se encontró en el spotLight, buscar en los hijos
            if (networkedSpotLight == null)
            {
                networkedSpotLight = GetComponentInChildren<NetworkedSpotLight>();
            }

            ValidateComponents();
            InitializeLights();
        }

        private void Start()
        {
            // Suscribirse a eventos de grab
            if (grabbable != null)
            {
                StartObservingGrabEvents().Forget();
            }

            // Configurar el botón
            if (flashlightButton != null)
            {
                flashlightButton.OnButtonPressed += OnButtonPressed;
                flashlightButton.OnButtonReleased += OnButtonReleased;
            }

            // Estado inicial: apagada
            SetLightState(false);
        }

        private void OnDestroy()
        {
            if (flashlightButton != null)
            {
                flashlightButton.OnButtonPressed -= OnButtonPressed;
                flashlightButton.OnButtonReleased -= OnButtonReleased;
            }
        }
        #endregion

        #region Network Lifecycle
        public override void Spawned()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Spawned. HasAuthority: {HasStateAuthority}");
            Debug.Log($"  NetworkedSpotLight found: {networkedSpotLight != null}");
            if (spotLight != null)
            {
                Debug.Log($"  SpotLight renderingLayerMask: {spotLight.renderingLayerMask}");
                Debug.Log($"  Normal Layer: {normalRenderingLayer}, UV Layer: {uvRenderingLayer}");
            }

            if (HasStateAuthority)
            {
                // Inicializar estado
                CurrentMode = FlashlightMode.Off;
                IsActive = false;
                CurrentHolder = PlayerRef.None;
                ButtonPressed = false;
                CurrentRenderingLayer = normalRenderingLayer; // Inicializar con el layer normal
            }

            // Aplicar estado inicial
            UpdateFlashlightState();
            isInitialized = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!isInitialized) return;

            // Monitorear el estado de grab
            UpdateGrabState();

            // Debug periódico
            if (Time.frameCount % 60 == 0 && IsActive)
            {
                Debug.Log($"[NetworkedFlashlight] {name} - Mode: {CurrentMode}, Active: {IsActive}, Holder: {CurrentHolder}");
            }
        }

        public override void Render()
        {
            // Actualización visual suave en el cliente
            if (spotLight != null && IsActive)
            {
                // Interpolar intensidad para transiciones suaves
                float targetIntensity = IsActive ? lightIntensity : 0f;
                spotLight.intensity = Mathf.Lerp(spotLight.intensity, targetIntensity, Time.deltaTime * 10f);
            }
        }
        #endregion

        #region Grab Management
        private async UniTaskVoid StartObservingGrabEvents()
        {
            await UniTask.WaitUntil(() => grabbable != null);

            while (this != null && gameObject != null)
            {
                // Revisar estado de grab cada frame
                CheckGrabState();
                await UniTask.Yield();
            }
        }

        private void CheckGrabState()
        {
            if (!HasStateAuthority) return;

            bool isCurrentlyGrabbed = grabbable.IsGrabbed;
            PlayerRef currentGrabber = grabbable.CurrentGrabber;

            // Detectar cambio en el estado de grab
            if (isCurrentlyGrabbed && !IsActive)
            {
                OnGrabbed(currentGrabber);
            }
            else if (!isCurrentlyGrabbed && IsActive)
            {
                OnReleased();
            }
        }

        private void UpdateGrabState()
        {
            if (!HasStateAuthority) return;

            // Sincronizar con el estado del FusionVRGrabbable
            if (grabbable != null && grabbable.IsGrabbed != IsActive)
            {
                if (grabbable.IsGrabbed)
                {
                    OnGrabbed(grabbable.CurrentGrabber);
                }
                else
                {
                    OnReleased();
                }
            }
        }

        private void OnGrabbed(PlayerRef grabber)
        {
            if (!HasStateAuthority) return;

            Debug.Log($"[NetworkedFlashlight] {name} - Grabbed by {grabber}");

            // Actualizar estado networkeado
            CurrentHolder = grabber;
            IsActive = true;
            CurrentMode = FlashlightMode.WhiteLight; // Empezar con luz blanca
            CurrentRenderingLayer = normalRenderingLayer; // Asegurar que empiece con el layer correcto

            // Reproducir sonido local si somos el que agarra
            if (Runner.LocalPlayer == grabber)
            {
                PlaySound(grabSound);
            }
        }

        private void OnReleased()
        {
            if (!HasStateAuthority) return;

            Debug.Log($"[NetworkedFlashlight] {name} - Released");

            // Reproducir sonido local si éramos el que sostenía
            if (Runner.LocalPlayer == CurrentHolder)
            {
                PlaySound(releaseSound);
            }

            // Actualizar estado networkeado
            CurrentHolder = PlayerRef.None;
            IsActive = false;
            CurrentMode = FlashlightMode.Off;
            // NO resetear CurrentRenderingLayer - mantener el último usado
        }
        #endregion

        #region Button Interaction
        private void OnButtonPressed()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Button pressed locally. HasAuthority: {HasStateAuthority}, IsActive: {IsActive}, CurrentHolder: {CurrentHolder}, LocalPlayer: {Runner.LocalPlayer}");

            // Solo el jugador que sostiene la linterna puede cambiar el modo
            // Verificamos que tengamos autoridad O que seamos el jugador que la sostiene
            if (IsActive && CurrentHolder == Runner.LocalPlayer)
            {
                if (HasStateAuthority)
                {
                    // Si tenemos autoridad, cambiar directamente
                    ToggleFlashlightMode();
                }
                else
                {
                    // Si no tenemos autoridad, solicitar el cambio al host
                    RPC_RequestModeChange();
                }
            }
        }

        private void OnButtonReleased()
        {
            if (HasStateAuthority)
            {
                ButtonPressed = false;
            }
        }

        // RPC para que los clientes soliciten cambio de modo al host
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestModeChange()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - RPC Mode change requested from client");

            // Verificar que el solicitante sea quien tiene la linterna
            if (IsActive)
            {
                ToggleFlashlightMode();
            }
        }

        // Método para cambiar el modo (solo ejecutado por quien tiene autoridad)
        private void ToggleFlashlightMode()
        {
            if (!HasStateAuthority) return;

            Debug.Log($"[NetworkedFlashlight] {name} - Toggling mode from {CurrentMode}");

            // Cambiar entre luz blanca y UV
            FlashlightMode newMode = CurrentMode;
            uint newRenderingLayer = normalRenderingLayer; // Default

            if (CurrentMode == FlashlightMode.WhiteLight)
            {
                newMode = FlashlightMode.UVLight;
                newRenderingLayer = uvRenderingLayer;
            }
            else if (CurrentMode == FlashlightMode.UVLight)
            {
                newMode = FlashlightMode.WhiteLight;
                newRenderingLayer = normalRenderingLayer;
            }

            // Actualizar el modo y rendering layer networkeados
            CurrentMode = newMode;
            CurrentRenderingLayer = newRenderingLayer;
            ButtonPressed = true;

            Debug.Log($"[NetworkedFlashlight] Mode changed to: {newMode}, RenderingLayer: {newRenderingLayer}");

            // Notificar a todos los jugadores del cambio
            RPC_NotifyModeChanged(newMode);
        }

        // RPC para notificar a todos los jugadores del cambio de modo
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyModeChanged(FlashlightMode newMode)
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Mode changed to {newMode} (notified via RPC)");

            // Reproducir sonido para todos los jugadores
            PlaySound(switchSound);

            // Forzar actualización visual inmediata
            UpdateFlashlightState();
        }
        #endregion

        #region State Management
        private void OnFlashlightModeChanged()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Mode changed to: {CurrentMode}");

            // Actualizar el rendering layer basado en el nuevo modo
            if (!HasStateAuthority)
            {
                // Los clientes deben sincronizar el rendering layer con el modo
                switch (CurrentMode)
                {
                    case FlashlightMode.WhiteLight:
                        CurrentRenderingLayer = normalRenderingLayer;
                        break;
                    case FlashlightMode.UVLight:
                        CurrentRenderingLayer = uvRenderingLayer;
                        break;
                    // Para Off, mantener el último layer
                }
            }

            UpdateFlashlightState();

            // Feedback visual adicional
            if (CurrentMode != lastMode)
            {
                OnModeTransition(lastMode, CurrentMode);
                lastMode = CurrentMode;
            }
        }

        private void OnIsActiveChanged()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Active state changed to: {IsActive}");

            UpdateFlashlightState();
        }

        private void OnRenderingLayerChanged()
        {
            Debug.Log($"[NetworkedFlashlight] {name} - Rendering layer changed to: {CurrentRenderingLayer} (Expected for mode: {CurrentMode})");

            // Aplicar el cambio de rendering layer directamente
            if (spotLight != null)
            {
                int layerMask = (int)(1U << (int)CurrentRenderingLayer);
                spotLight.renderingLayerMask = layerMask;

                Debug.Log($"[NetworkedFlashlight] Applied renderingLayerMask: {layerMask} (Binary: {System.Convert.ToString(layerMask, 2)})");

                // Sincronizar con NetworkedSpotLight si existe
                if (networkedSpotLight != null && HasStateAuthority)
                {
                    uint networkLayerMask = 1U << (int)CurrentRenderingLayer;
                    Color currentColor = (CurrentMode == FlashlightMode.UVLight) ? uvLightColor : whiteLightColor;
                    float currentIntensity = (CurrentMode == FlashlightMode.UVLight) ? lightIntensity * 1.5f : lightIntensity;

                    networkedSpotLight.UpdateLightSettings(
                        IsActive && CurrentMode != FlashlightMode.Off,
                        currentColor,
                        IsActive ? currentIntensity : 0f,
                        networkLayerMask
                    );
                }
            }
        }

        private void UpdateFlashlightState()
        {
            if (spotLight == null) return;

            Debug.Log($"[NetworkedFlashlight] UpdateFlashlightState - Mode: {CurrentMode}, Active: {IsActive}, CurrentRenderingLayer: {CurrentRenderingLayer}");

            // Actualizar estado de la luz
            SetLightState(IsActive && CurrentMode != FlashlightMode.Off);

            if (IsActive)
            {
                switch (CurrentMode)
                {
                    case FlashlightMode.WhiteLight:
                        ApplyWhiteLightSettings();
                        break;

                    case FlashlightMode.UVLight:
                        ApplyUVLightSettings();
                        break;

                    case FlashlightMode.Off:
                        SetLightState(false);
                        break;
                }
            }
        }

        private void ApplyWhiteLightSettings()
        {
            if (spotLight == null) return;

            spotLight.color = whiteLightColor;
            // Usar el CurrentRenderingLayer networkeado
            int layerMask = (int)(1U << (int)CurrentRenderingLayer);
            spotLight.renderingLayerMask = layerMask;
            spotLight.intensity = lightIntensity;

            // Si tenemos un NetworkedSpotLight, actualizarlo también
            if (networkedSpotLight != null && HasStateAuthority)
            {
                uint networkLayerMask = 1U << (int)CurrentRenderingLayer;
                networkedSpotLight.UpdateLightSettings(
                    true,
                    whiteLightColor,
                    lightIntensity,
                    networkLayerMask  // Pasar el mask calculado
                );

                Debug.Log($"[NetworkedFlashlight] Updating NetworkedSpotLight - Layer: {CurrentRenderingLayer}, Mask: {networkLayerMask}");
            }

            // Actualizar materiales
            if (flashlightBodyRenderer != null && whiteLightMaterial != null)
            {
                flashlightBodyRenderer.material = whiteLightMaterial;
            }

            // Desactivar indicador UV
            if (uvIndicator != null)
            {
                uvIndicator.SetActive(false);
            }

            Debug.Log($"[NetworkedFlashlight] {name} - Applied white light settings.");
            Debug.Log($"  CurrentRenderingLayer: {CurrentRenderingLayer}");
            Debug.Log($"  RenderLayer Mask: {spotLight.renderingLayerMask} (Binary: {System.Convert.ToString((long)spotLight.renderingLayerMask, 2)})");
        }

        private void ApplyUVLightSettings()
        {
            if (spotLight == null) return;

            spotLight.color = uvLightColor;
            // Usar el CurrentRenderingLayer networkeado
            int layerMask = (int)(1U << (int)CurrentRenderingLayer);
            spotLight.renderingLayerMask = layerMask;
            spotLight.intensity = lightIntensity * 1.5f; // UV un poco más intensa

            // Si tenemos un NetworkedSpotLight, actualizarlo también
            if (networkedSpotLight != null && HasStateAuthority)
            {
                uint networkLayerMask = 1U << (int)CurrentRenderingLayer;
                networkedSpotLight.UpdateLightSettings(
                    true,
                    uvLightColor,
                    lightIntensity * 1.5f,
                    networkLayerMask
                );

                Debug.Log($"[NetworkedFlashlight] Updating NetworkedSpotLight UV - Layer: {CurrentRenderingLayer}, Mask: {networkLayerMask}");
            }

            // Actualizar materiales
            if (flashlightBodyRenderer != null && uvLightMaterial != null)
            {
                flashlightBodyRenderer.material = uvLightMaterial;
            }

            // Activar indicador UV
            if (uvIndicator != null)
            {
                uvIndicator.SetActive(true);
            }

            Debug.Log($"[NetworkedFlashlight] {name} - Applied UV light settings.");
            Debug.Log($"  CurrentRenderingLayer: {CurrentRenderingLayer}");
            Debug.Log($"  RenderLayer Mask: {spotLight.renderingLayerMask} (Binary: {System.Convert.ToString((long)spotLight.renderingLayerMask, 2)})");
        }

        private void SetLightState(bool enabled)
        {
            if (spotLight != null)
            {
                spotLight.enabled = enabled;

                if (!enabled)
                {
                    spotLight.intensity = 0f;

                    // Si tenemos un NetworkedSpotLight, desactivarlo también
                    if (networkedSpotLight != null && HasStateAuthority)
                    {
                        // Mantener el layer actual aunque esté apagada
                        uint currentMask = 1U << (int)CurrentRenderingLayer;
                        networkedSpotLight.UpdateLightSettings(
                            false,
                            spotLight.color,
                            0f,
                            currentMask
                        );

                        Debug.Log($"[NetworkedFlashlight] NetworkedSpotLight disabled - Layer: {CurrentRenderingLayer}, Mask: {currentMask}");
                    }

                    // Desactivar indicador UV
                    if (uvIndicator != null)
                    {
                        uvIndicator.SetActive(false);
                    }
                }
            }
        }

        private void OnModeTransition(FlashlightMode from, FlashlightMode to)
        {
            // Aquí se pueden agregar efectos de transición
            // Por ejemplo, un parpadeo rápido o cambio gradual
            Debug.Log($"[NetworkedFlashlight] {name} - Transition from {from} to {to}");
        }
        #endregion

        #region Light Configuration
        private void InitializeLights()
        {
            if (spotLight != null)
            {
                spotLight.type = LightType.Spot;
                spotLight.range = lightRange;
                spotLight.spotAngle = lightAngle;
                spotLight.intensity = 0f; // Empezar apagada
                spotLight.enabled = false;

                // Configurar sombras
                spotLight.shadows = LightShadows.Soft;
                spotLight.shadowStrength = 0.8f;

                Debug.Log($"[NetworkedFlashlight] {name} - Light initialized");
            }
        }
        #endregion

        #region Audio
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        #endregion

        #region Validation
        private void ValidateComponents()
        {
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>();
                if (spotLight == null)
                {
                    Debug.LogError($"[NetworkedFlashlight] {name} - No Light component found!");
                }
                else
                {
                    Debug.Log($"[NetworkedFlashlight] {name} - Light found. Current renderingLayerMask: {spotLight.renderingLayerMask}");
                }
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f; // 3D sound
                    audioSource.maxDistance = 10f;
                }
            }

            var networkObject = GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[NetworkedFlashlight] {name} - NetworkObject component required!");
            }

            if (grabbable == null)
            {
                Debug.LogError($"[NetworkedFlashlight] {name} - FusionVRGrabbable component required!");
            }

            // Información sobre NetworkedSpotLight
            if (networkedSpotLight != null)
            {
                Debug.Log($"[NetworkedFlashlight] {name} - NetworkedSpotLight component found and will be synchronized");
            }
            else
            {
                Debug.Log($"[NetworkedFlashlight] {name} - No NetworkedSpotLight component found. Light will be updated locally only.");
                Debug.Log($"  To enable networked light sync, add NetworkedSpotLight component to the light GameObject");
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (spotLight == null) return;

            // Visualizar el cono de luz
            Gizmos.color = IsActive ?
                (CurrentMode == FlashlightMode.UVLight ? uvLightColor : whiteLightColor) :
                Color.gray;

            Vector3 origin = spotLight.transform.position;
            Vector3 direction = spotLight.transform.forward;
            float angle = spotLight.spotAngle * 0.5f * Mathf.Deg2Rad;
            float range = spotLight.range;

            // Dibujar cono
            for (int i = 0; i < 8; i++)
            {
                float rotAngle = (360f / 8f) * i * Mathf.Deg2Rad;
                Vector3 rayDirection = Quaternion.AngleAxis(rotAngle * Mathf.Rad2Deg, direction) *
                    Quaternion.AngleAxis(angle * Mathf.Rad2Deg, spotLight.transform.right) * direction;
                Gizmos.DrawRay(origin, rayDirection * range);
            }

            // Dibujar círculo al final del cono
            Gizmos.DrawWireSphere(origin + direction * range, range * Mathf.Tan(angle));
        }
        #endregion
    }
}

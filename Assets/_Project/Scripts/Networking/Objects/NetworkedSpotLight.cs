using UnityEngine;
using Fusion;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Componente para sincronizar una luz Spot a través de la red
    /// Se usa como hijo del NetworkedFlashlight para sincronización independiente
    /// </summary>
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedSpotLight : NetworkBehaviour
    {
        #region Serialized Fields
        [Header("Light Reference")]
        [SerializeField] private Light spotLight;
        #endregion

        #region Networked Properties
        // Sincronizar propiedades de la luz
        [Networked, OnChangedRender(nameof(OnLightEnabledChanged))]
        public NetworkBool LightEnabled { get; set; }

        [Networked, OnChangedRender(nameof(OnLightPropertiesChanged))]
        public Color LightColor { get; set; }

        [Networked, OnChangedRender(nameof(OnLightPropertiesChanged))]
        public float LightIntensity { get; set; }

        [Networked, OnChangedRender(nameof(OnLightPropertiesChanged))]
        public float LightRange { get; set; }

        [Networked, OnChangedRender(nameof(OnLightPropertiesChanged))]
        public float LightSpotAngle { get; set; }

        [Networked, OnChangedRender(nameof(OnRenderLayerChanged))]
        public uint RenderingLayerMask { get; set; }
        #endregion

        #region Private Fields
        private NetworkedFlashlight parentFlashlight;
        private bool isInitialized = false;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (spotLight == null)
            {
                spotLight = GetComponent<Light>();
            }

            parentFlashlight = GetComponentInParent<NetworkedFlashlight>();

            ValidateSetup();
        }
        #endregion

        #region Network Lifecycle
        public override void Spawned()
        {
            AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Spawned. HasAuthority: {HasStateAuthority}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);

            if (HasStateAuthority && spotLight != null)
            {
                // Inicializar valores networkeados desde la luz actual
                InitializeNetworkedProperties();
            }

            isInitialized = true;
            ApplyNetworkedProperties();
        }

        public override void FixedUpdateNetwork()
        {
            if (!isInitialized || !HasStateAuthority) return;

            // Sincronizar propiedades si cambian localmente
            SyncLightProperties();
        }
        #endregion

        #region Synchronization
        private void InitializeNetworkedProperties()
        {
            if (spotLight == null) return;

            LightEnabled = spotLight.enabled;
            LightColor = spotLight.color;
            LightIntensity = spotLight.intensity;
            LightRange = spotLight.range;
            LightSpotAngle = spotLight.spotAngle;
            // Convertir int a uint de manera segura
            RenderingLayerMask = (uint)spotLight.renderingLayerMask;

            AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Initialized network properties", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"  Enabled: {LightEnabled}, Color: {LightColor}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"  Intensity: {LightIntensity}, Range: {LightRange}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"  RenderLayer: {RenderingLayerMask}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }

        private void SyncLightProperties()
        {
            if (spotLight == null) return;

            // Solo sincronizar si hay cambios significativos
            bool hasChanges = false;

            if (spotLight.enabled != LightEnabled)
            {
                LightEnabled = spotLight.enabled;
                hasChanges = true;
            }

            if (!ColorsAreEqual(spotLight.color, LightColor))
            {
                LightColor = spotLight.color;
                hasChanges = true;
            }

            if (Mathf.Abs(spotLight.intensity - LightIntensity) > 0.01f)
            {
                LightIntensity = spotLight.intensity;
                hasChanges = true;
            }

            if (Mathf.Abs(spotLight.range - LightRange) > 0.01f)
            {
                LightRange = spotLight.range;
                hasChanges = true;
            }

            if (Mathf.Abs(spotLight.spotAngle - LightSpotAngle) > 0.1f)
            {
                LightSpotAngle = spotLight.spotAngle;
                hasChanges = true;
            }

            // Comparar y actualizar con conversión segura
            uint currentMask = (uint)spotLight.renderingLayerMask;
            if (currentMask != RenderingLayerMask)
            {
                RenderingLayerMask = currentMask;
                hasChanges = true;
            }

            if (hasChanges)
            {
                AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Properties synchronized", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            }
        }

        public void UpdateLightSettings(bool enabled, Color color, float intensity, uint renderLayer)
        {
            if (!HasStateAuthority) return;

            LightEnabled = enabled;
            LightColor = color;
            LightIntensity = intensity;
            RenderingLayerMask = renderLayer;

            // Aplicar localmente también
            ApplyNetworkedProperties();

            AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Settings updated manually", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"  Enabled: {enabled}, Color: {color}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            AdvancedDebugSystem.Log($"  Intensity: {intensity}, RenderLayer: {renderLayer}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }
        #endregion

        #region Network Callbacks
        private void OnLightEnabledChanged()
        {
            if (spotLight != null && !HasStateAuthority)
            {
                spotLight.enabled = LightEnabled;

                AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Light enabled changed to: {LightEnabled}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            }
        }

        private void OnLightPropertiesChanged()
        {
            if (!HasStateAuthority)
            {
                ApplyNetworkedProperties();
            }
        }

        private void OnRenderLayerChanged()
        {
            if (spotLight != null && !HasStateAuthority)
            {
                // Convertir uint a int de manera segura
                spotLight.renderingLayerMask = (int)RenderingLayerMask;

                AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Render layer changed to: {RenderingLayerMask}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            }
        }

        private void ApplyNetworkedProperties()
        {
            if (spotLight == null) return;

            spotLight.enabled = LightEnabled;
            spotLight.color = LightColor;
            spotLight.intensity = LightIntensity;
            spotLight.range = LightRange;
            spotLight.spotAngle = LightSpotAngle;
            // Convertir uint a int de manera segura
            spotLight.renderingLayerMask = (int)RenderingLayerMask;

            if (Time.frameCount % 120 == 0)
            {
                AdvancedDebugSystem.Log($"[NetworkedSpotLight] {name} - Properties applied", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
                AdvancedDebugSystem.Log($"  Enabled: {LightEnabled}, Color: {LightColor}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
                AdvancedDebugSystem.Log($"  RenderLayer: {RenderingLayerMask}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
            }
        }
        #endregion

        #region Helper Methods
        private bool ColorsAreEqual(Color a, Color b, float tolerance = 0.01f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance &&
                   Mathf.Abs(a.a - b.a) < tolerance;
        }
        #endregion

        #region Validation
        private void ValidateSetup()
        {
            if (spotLight == null)
            {
                AdvancedDebugSystem.LogError($"[NetworkedSpotLight] {name} - Light component required!", LogCategory.Flashlight | LogCategory.Photon);
            }
            else if (spotLight.type != LightType.Spot)
            {
                AdvancedDebugSystem.LogWarning($"[NetworkedSpotLight] {name} - Light is not a Spot light. Type: {spotLight.type}", LogCategory.Flashlight | LogCategory.Photon);
            }

            var networkObject = GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                AdvancedDebugSystem.LogError($"[NetworkedSpotLight] {name} - NetworkObject component required!", LogCategory.Flashlight | LogCategory.Photon);
            }

            if (parentFlashlight == null)
            {
                AdvancedDebugSystem.LogWarning($"[NetworkedSpotLight] {name} - No parent NetworkedFlashlight found. This component works best as a child of NetworkedFlashlight.", LogCategory.Flashlight | LogCategory.Photon);
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (spotLight == null) return;

            // Visualizar el estado de sincronización
            Gizmos.color = LightEnabled ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            // Mostrar el layer de renderizado
            if (LightEnabled)
            {
                Gizmos.color = new Color(LightColor.r, LightColor.g, LightColor.b, 0.3f);
                Vector3 direction = transform.forward;
                float angle = LightSpotAngle * 0.5f * Mathf.Deg2Rad;
                float range = LightRange;

                // Dibujar líneas del cono
                for (int i = 0; i < 4; i++)
                {
                    float rotAngle = 90f * i * Mathf.Deg2Rad;
                    Vector3 rayDirection = Quaternion.AngleAxis(rotAngle * Mathf.Rad2Deg, direction) *
                                           Quaternion.AngleAxis(angle * Mathf.Rad2Deg, transform.right) * direction;
                    Gizmos.DrawRay(transform.position, rayDirection * range);
                }
            }
        }
        #endregion
    }
}

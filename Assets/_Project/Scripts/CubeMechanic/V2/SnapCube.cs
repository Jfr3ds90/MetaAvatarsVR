using UnityEngine;
using Meta.XR.MRUtilityKit;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System.Collections;

namespace PuzzleCubes.Core
{
    /// <summary>
    /// Cubo interactivo con sistema de snap magnético
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class SnapCube : MonoBehaviour
    {
        [Header("Snap Settings")]
        [SerializeField] private float snapDistance = 0.05f;
        [SerializeField] private float snapForce = 10f;
        [SerializeField] private LayerMask snapLayer;
        
        [Header("Visual Feedback")]
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Material snapPreviewMaterial;
        [SerializeField] private Material snappedMaterial;
        [SerializeField] private GameObject snapIndicator;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip snapSound;
        [SerializeField] private AudioClip releaseSound;
        
        [Header("Particle Effects")]
        [SerializeField] private ParticleSystem snapParticles;
        [SerializeField] private bool createParticlesIfMissing = true;
        [SerializeField] private float particleDuration = 0.5f;
        
        private Rigidbody rb;
        private MeshRenderer meshRenderer;
        private Grabbable grabbable;
        
        private CubeGrid cubeGrid;
        private Vector3Int gridPosition;
        private bool isSnapped = false;
        private bool isBeingGrabbed = false;
        private Vector3 snapTargetPosition;
        private bool hasSnapTarget = false;
        
        // Estados para el feedback visual
        private enum CubeState { Default, Previewing, Snapped }
        private CubeState currentState = CubeState.Default;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            meshRenderer = GetComponent<MeshRenderer>();
            grabbable = GetComponent<Grabbable>();
            
            if (snapIndicator)
                snapIndicator.SetActive(false);
            
            // Configurar Rigidbody para VR
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            
            // Suscribirse a eventos de grab
            if (grabbable)
            {
                grabbable.WhenPointerEventRaised += OnPointerEvent;
            }
            
            // Setup particles si no existen
            SetupSnapParticles();
        }
        
        private void Start()
        {
            cubeGrid = FindAnyObjectByType<CubeGrid>();
            if (!cubeGrid)
            {
                Debug.LogError("CubeGrid not found in scene!");
            }
        }
        
        private void OnDestroy()
        {
            if (grabbable)
            {
                grabbable.WhenPointerEventRaised -= OnPointerEvent;
            }
            
            // Desregistrar de la grilla si está snapped
            if (isSnapped && cubeGrid)
            {
                cubeGrid.UnregisterCube(gridPosition);
            }
        }
        
        private void OnPointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    OnGrabbed();
                    break;
                case PointerEventType.Unselect:
                    OnReleased();
                    break;
            }
        }
        
        private void OnGrabbed()
        {
            isBeingGrabbed = true;
            
            // Si estaba snapped, desregistrar de la grilla
            if (isSnapped && cubeGrid)
            {
                cubeGrid.UnregisterCube(gridPosition);
                isSnapped = false;
                
                // Reproducir sonido de release
                if (audioSource && releaseSound)
                {
                    audioSource.PlayOneShot(releaseSound);
                }
            }
            
            // Activar física nuevamente (independientemente de si estaba snapped o no)
            rb.isKinematic = false;
            rb.useGravity = true;
            
            UpdateVisualState(CubeState.Default);
        }
        
        private void OnReleased()
        {
            isBeingGrabbed = false;
            
            // Intentar snap si hay una posición válida
            if (hasSnapTarget && cubeGrid)
            {
                SnapToPosition();
            }
            else
            {
                // No hay posición de snap válida, resetear al estado default
                ResetToDefaultState();
            }
        }
        
        private void Update()
        {
            if (!cubeGrid) return;
            
            // Solo buscar snap positions cuando está siendo agarrado
            if (isBeingGrabbed)
            {
                // Buscar posición de snap cercana
                var (found, gridPos, worldPos) = cubeGrid.FindNearestSnapPosition(transform.position, snapDistance);
                
                if (found)
                {
                    hasSnapTarget = true;
                    snapTargetPosition = worldPos;
                    gridPosition = gridPos;
                    
                    // Mostrar preview
                    if (snapIndicator)
                    {
                        snapIndicator.SetActive(true);
                        snapIndicator.transform.position = worldPos;
                    }
                    
                    UpdateVisualState(CubeState.Previewing);
                }
                else
                {
                    hasSnapTarget = false;
                    
                    if (snapIndicator)
                        snapIndicator.SetActive(false);
                    
                    UpdateVisualState(CubeState.Default);
                }
            }
        }
        
        private void FixedUpdate()
        {
            // Aplicar fuerza de snap si está cerca de una posición válida
            if (isBeingGrabbed && hasSnapTarget && !isSnapped)
            {
                Vector3 direction = snapTargetPosition - transform.position;
                float distance = direction.magnitude;
                
                if (distance < snapDistance && distance > 0.001f)
                {
                    // Aplicar fuerza suave hacia la posición de snap
                    rb.AddForce(direction.normalized * snapForce * (1f - distance / snapDistance), ForceMode.Force);
                }
            }
        }
        
        private void SnapToPosition()
        {
            if (!cubeGrid || !hasSnapTarget) return;
            
            // Intentar registrar en la grilla
            if (cubeGrid.RegisterCube(this, gridPosition))
            {
                // Posicionar exactamente
                transform.position = snapTargetPosition;
                transform.rotation = Quaternion.identity; // Alinear con la grilla
                
                // Desactivar física
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                isSnapped = true;
                
                // Feedback
                UpdateVisualState(CubeState.Snapped);
                
                if (audioSource && snapSound)
                {
                    audioSource.PlayOneShot(snapSound);
                }
                
                // Haptic feedback
                TriggerHapticFeedback();
                
                // Particle effect
                PlaySnapParticles();
            }
            
            // Ocultar indicador
            if (snapIndicator)
                snapIndicator.SetActive(false);
        }
        
        private void UpdateVisualState(CubeState newState)
        {
            if (currentState == newState || !meshRenderer) return;
            
            currentState = newState;
            
            switch (newState)
            {
                case CubeState.Default:
                    if (defaultMaterial)
                        meshRenderer.material = defaultMaterial;
                    break;
                    
                case CubeState.Previewing:
                    if (snapPreviewMaterial)
                        meshRenderer.material = snapPreviewMaterial;
                    break;
                    
                case CubeState.Snapped:
                    if (snappedMaterial)
                        meshRenderer.material = snappedMaterial;
                    break;
            }
        }
        
        private void TriggerHapticFeedback()
        {
            // Implementar haptic feedback usando OVRInput
            if (OVRInput.GetActiveController() != OVRInput.Controller.None)
            {
                OVRInput.SetControllerVibration(0.3f, 0.5f, OVRInput.GetActiveController());
            }
        }
        
        /// <summary>
        /// Configura el sistema de partículas si no existe
        /// </summary>
        private void SetupSnapParticles()
        {
            if (!snapParticles && createParticlesIfMissing)
            {
                // Crear GameObject para las partículas
                GameObject particleObj = new GameObject("SnapParticles");
                particleObj.transform.SetParent(transform);
                particleObj.transform.localPosition = Vector3.zero;
                
                // Añadir y configurar ParticleSystem
                snapParticles = particleObj.AddComponent<ParticleSystem>();
                
                // Configuración principal
                var main = snapParticles.main;
                main.duration = particleDuration;
                main.startLifetime = 0.5f;
                main.startSpeed = 2f;
                main.startSize = 0.05f;
                main.startColor = new Color(0.5f, 0.8f, 1f, 1f);
                main.maxParticles = 30;
                main.playOnAwake = false;
                main.loop = false;
                
                // Emisión
                var emission = snapParticles.emission;
                emission.enabled = true;
                emission.SetBursts(new ParticleSystem.Burst[]
                {
                    new ParticleSystem.Burst(0.0f, 30)
                });
                
                // Forma
                var shape = snapParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;
                
                // Velocidad sobre tiempo
                var velocityOverLifetime = snapParticles.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(2f);
                
                // Tamaño sobre tiempo
                var sizeOverLifetime = snapParticles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0f, 0.5f);
                sizeCurve.AddKey(0.5f, 1f);
                sizeCurve.AddKey(1f, 0f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
                
                // Color sobre tiempo (fade out)
                var colorOverLifetime = snapParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(Color.white, 0.0f), 
                        new GradientColorKey(Color.white, 1.0f) 
                    },
                    new GradientAlphaKey[] { 
                        new GradientAlphaKey(1.0f, 0.0f), 
                        new GradientAlphaKey(0.0f, 1.0f) 
                    }
                );
                colorOverLifetime.color = gradient;
                
                // Renderer
                var renderer = snapParticles.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
        
        /// <summary>
        /// Reproduce el efecto de partículas
        /// </summary>
        private void PlaySnapParticles()
        {
            if (snapParticles)
            {
                snapParticles.gameObject.SetActive(true);
                snapParticles.Stop();
                snapParticles.Play();
                
                // Programar para detener después de la duración
                StartCoroutine(StopParticlesAfterDuration());
            }
        }
        
        /// <summary>
        /// Detiene las partículas después de su duración
        /// </summary>
        private IEnumerator StopParticlesAfterDuration()
        {
            yield return new WaitForSeconds(particleDuration);
            
            if (snapParticles)
            {
                snapParticles.Stop();
                snapParticles.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// Resetea el cubo al estado default
        /// </summary>
        private void ResetToDefaultState()
        {
            // Asegurarse de que no está marcado como snapped
            isSnapped = false;
            hasSnapTarget = false;
            
            // Resetear física
            rb.isKinematic = false;
            rb.useGravity = true;
            
            // Resetear visual
            UpdateVisualState(CubeState.Default);
            
            // Ocultar indicador si existe
            if (snapIndicator)
                snapIndicator.SetActive(false);
        }
        
        /// <summary>
        /// Obtiene la posición en la grilla
        /// </summary>
        public Vector3Int GetGridPosition() => gridPosition;
        
        /// <summary>
        /// Verifica si el cubo está snapped
        /// </summary>
        public bool IsSnapped() => isSnapped;
        
        #region Editor Helper
        
        private void OnValidate()
        {
            // Auto-configurar capas si es necesario
            if (snapLayer == 0)
            {
                snapLayer = LayerMask.GetMask("Default");
            }
            
            // Asegurar que el cubo tenga el tamaño correcto
            transform.localScale = Vector3.one * 0.1f;
        }
        
        #endregion
    }
}
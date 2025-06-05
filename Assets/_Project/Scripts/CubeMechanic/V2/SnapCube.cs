using UnityEngine;
using Oculus.Interaction;

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
        }
        
        private void Start()
        {
            cubeGrid = FindObjectOfType<CubeGrid>();
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
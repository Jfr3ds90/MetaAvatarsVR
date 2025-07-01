using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using DG.Tweening;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Slider 3D interactuable para VR usando Meta Interaction SDK
    /// Permite ajustar valores mediante drag con el rayo o controladores
    /// </summary>
    [System.Serializable]
    public class SliderValueChangedEvent : UnityEvent<float> { }

    public class InteractableSlider3D : MonoBehaviour
    {
        [Header("Slider Components")]
        [SerializeField] private Transform sliderTrack;
        [SerializeField] private Transform sliderHandle;
        [SerializeField] private Transform fillBar;
        [SerializeField] private TextMeshProUGUI valueLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        
        [Header("Interaction")]
        [SerializeField] private RayInteractable handleInteractable;
        [SerializeField] private ColliderSurface handleSurface;
        [SerializeField] private float trackLength = 1f;
        
        [Header("Slider Settings")]
        [SerializeField] private string sliderTitle = "Slider";
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 1f;
        [SerializeField] private float currentValue = 0.5f;
        [SerializeField] private bool wholeNumbers = false;
        [SerializeField] private string valueFormat = "F2"; // Format for float display
        
        [Header("Visual Settings")]
        [SerializeField] private Material trackMaterial;
        [SerializeField] private Material fillMaterial;
        [SerializeField] private Material handleNormalMaterial;
        [SerializeField] private Material handleHoverMaterial;
        [SerializeField] private Material handleDragMaterial;
        [SerializeField] private Gradient fillGradient;
        
        [Header("Animation")]
        [SerializeField] private float handleHoverScale = 1.2f;
        [SerializeField] private float handlePressScale = 1.5f;
        [SerializeField] private float animationDuration = 0.15f;
        [SerializeField] private Ease animationEase = Ease.OutBack;
        
        [Header("Haptic Feedback")]
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private float snapHapticStrength = 0.1f;
        [SerializeField] private int notchCount = 0; // 0 = continuous, >0 = discrete steps
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip dragSound;
        [SerializeField] private AudioClip snapSound;
        
        [Header("Events")]
        public SliderValueChangedEvent OnValueChanged;
        public UnityEvent OnSliderPressed;
        public UnityEvent OnSliderReleased;
        
        // Private state
        private bool _isDragging = false;
        private bool _isHovered = false;
        private Vector3 _dragStartPoint;
        private float _dragStartValue;
        private RayInteractor _activeInteractor;
        private Vector3 _handleOriginalScale;
        private Renderer _handleRenderer;
        private Renderer _fillRenderer;
        
        // Cached calculations
        private float _normalizedValue;
        private Vector3 _trackStartPos;
        private Vector3 _trackEndPos;
        private Vector3 _trackDirection;
        
        private void Awake()
        {
            InitializeComponents();
            SetupInteraction();
            UpdateSliderVisuals();
        }
        
        private void InitializeComponents()
        {
            // Cache renderers
            if (sliderHandle != null)
            {
                _handleRenderer = sliderHandle.GetComponent<Renderer>();
                _handleOriginalScale = sliderHandle.localScale;
            }
            
            if (fillBar != null)
            {
                _fillRenderer = fillBar.GetComponent<Renderer>();
            }
            
            // Calculate track positions
            if (sliderTrack != null)
            {
                _trackStartPos = sliderTrack.position - sliderTrack.right * (trackLength / 2f);
                _trackEndPos = sliderTrack.position + sliderTrack.right * (trackLength / 2f);
                _trackDirection = (_trackEndPos - _trackStartPos).normalized;
            }
            
            // Set initial title
            if (titleLabel != null)
            {
                titleLabel.text = sliderTitle;
            }
            
            // Create audio source if needed
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f; // 3D sound
                audioSource.maxDistance = 5f;
            }
        }
        
        private void SetupInteraction()
        {
            // Setup handle interactable
            if (handleInteractable == null && sliderHandle != null)
            {
                handleInteractable = sliderHandle.gameObject.AddComponent<RayInteractable>();
            }
            
            // Setup collision surface
            if (handleSurface == null && sliderHandle != null)
            {
                SphereCollider collider = sliderHandle.GetComponent<SphereCollider>();
                if (collider == null)
                {
                    collider = sliderHandle.gameObject.AddComponent<SphereCollider>();
                    collider.radius = 0.05f;
                    collider.isTrigger = true;
                }
                
                handleSurface = sliderHandle.gameObject.AddComponent<ColliderSurface>();
            }
            
            // Configure interactable
            if (handleInteractable != null && handleSurface != null)
            {
                handleInteractable.InjectSurface(handleSurface);
            }
        }
        
        private void Update()
        {
            CheckInteraction();
            
            if (_isDragging && _activeInteractor != null)
            {
                UpdateDragPosition();
            }
        }
        
        private void CheckInteraction()
        {
            if (handleInteractable == null) return;
            
            // Check for hover state
            bool isCurrentlyHovered = false;
            RayInteractor hoveredBy = null;
            
            // Get all ray interactors in scene (you might want to cache these)
            var rayInteractors = FindObjectsOfType<RayInteractor>();
            
            foreach (var interactor in rayInteractors)
            {
                if (interactor.HasCandidate && 
                    interactor.CandidateProperties is RayInteractor.RayCandidateProperties props &&
                    props.ClosestInteractable == handleInteractable)
                {
                    isCurrentlyHovered = true;
                    hoveredBy = interactor;
                    break;
                }
            }
            
            // Handle hover state changes
            if (isCurrentlyHovered != _isHovered)
            {
                _isHovered = isCurrentlyHovered;
                if (_isHovered)
                {
                    OnHoverEnter();
                }
                else
                {
                    OnHoverExit();
                }
            }
            
            // Handle drag start
            if (_isHovered && hoveredBy != null && !_isDragging)
            {
                if (hoveredBy.State == InteractorState.Select)
                {
                    StartDrag(hoveredBy);
                }
            }
            
            // Handle drag end
            if (_isDragging && _activeInteractor != null)
            {
                if (_activeInteractor.State != InteractorState.Select)
                {
                    EndDrag();
                }
            }
        }
        
        private void OnHoverEnter()
        {
            if (!enabled) return;
            
            // Visual feedback
            AnimateHandle(handleHoverScale);
            UpdateHandleMaterial(handleHoverMaterial);
            
            // Audio feedback
            if (audioSource != null && dragSound != null)
            {
                audioSource.PlayOneShot(dragSound, 0.5f);
            }
        }
        
        private void OnHoverExit()
        {
            if (_isDragging) return;
            
            // Reset visual state
            AnimateHandle(1f);
            UpdateHandleMaterial(handleNormalMaterial);
        }
        
        private void StartDrag(RayInteractor interactor)
        {
            _isDragging = true;
            _activeInteractor = interactor;
            
            // Store drag start info
            _dragStartPoint = GetRayPoint(interactor);
            _dragStartValue = currentValue;
            
            // Visual feedback
            AnimateHandle(handlePressScale);
            UpdateHandleMaterial(handleDragMaterial);
            
            // Haptic feedback
            if (enableHaptics)
            {
                TriggerHapticFeedback(0.3f);
            }
            
            OnSliderPressed?.Invoke();
        }
        
        private void UpdateDragPosition()
        {
            if (_activeInteractor == null || sliderHandle == null) return;
            
            // Get current ray point
            Vector3 currentRayPoint = GetRayPoint(_activeInteractor);
            
            // Project onto slider track
            Vector3 closestPoint = GetClosestPointOnTrack(currentRayPoint);
            
            // Calculate normalized position (0-1)
            float distance = Vector3.Distance(_trackStartPos, closestPoint);
            float totalLength = Vector3.Distance(_trackStartPos, _trackEndPos);
            _normalizedValue = Mathf.Clamp01(distance / totalLength);
            
            // Apply notches if configured
            if (notchCount > 0)
            {
                float notchSize = 1f / notchCount;
                float notchedValue = Mathf.Round(_normalizedValue / notchSize) * notchSize;
                
                // Haptic feedback on notch change
                if (enableHaptics && Mathf.Abs(notchedValue - _normalizedValue) < 0.01f)
                {
                    float oldNotched = Mathf.Round(GetNormalizedValue() / notchSize) * notchSize;
                    if (Mathf.Abs(notchedValue - oldNotched) > 0.01f)
                    {
                        TriggerHapticFeedback(snapHapticStrength);
                        PlaySnapSound();
                    }
                }
                
                _normalizedValue = notchedValue;
            }
            
            // Update value
            float newValue = Mathf.Lerp(minValue, maxValue, _normalizedValue);
            if (wholeNumbers)
            {
                newValue = Mathf.Round(newValue);
            }
            
            SetValue(newValue);
        }
        
        private void EndDrag()
        {
            _isDragging = false;
            _activeInteractor = null;
            
            // Visual feedback
            if (_isHovered)
            {
                AnimateHandle(handleHoverScale);
                UpdateHandleMaterial(handleHoverMaterial);
            }
            else
            {
                AnimateHandle(1f);
                UpdateHandleMaterial(handleNormalMaterial);
            }
            
            OnSliderReleased?.Invoke();
        }
        
        private Vector3 GetRayPoint(RayInteractor interactor)
        {
            if (interactor.CollisionInfo.HasValue)
            {
                return interactor.CollisionInfo.Value.Point;
            }
            return interactor.End;
        }
        
        private Vector3 GetClosestPointOnTrack(Vector3 point)
        {
            Vector3 trackToPoint = point - _trackStartPos;
            float dot = Vector3.Dot(trackToPoint, _trackDirection);
            dot = Mathf.Clamp(dot, 0f, trackLength);
            return _trackStartPos + _trackDirection * dot;
        }
        
        public void SetValue(float value)
        {
            float oldValue = currentValue;
            currentValue = Mathf.Clamp(value, minValue, maxValue);
            
            if (wholeNumbers)
            {
                currentValue = Mathf.Round(currentValue);
            }
            
            // Update normalized value
            _normalizedValue = Mathf.InverseLerp(minValue, maxValue, currentValue);
            
            // Update visuals
            UpdateSliderVisuals();
            
            // Fire event if value changed
            if (!Mathf.Approximately(oldValue, currentValue))
            {
                OnValueChanged?.Invoke(currentValue);
            }
        }
        
        private void UpdateSliderVisuals()
        {
            // Update handle position
            if (sliderHandle != null)
            {
                Vector3 handlePos = Vector3.Lerp(_trackStartPos, _trackEndPos, _normalizedValue);
                sliderHandle.position = handlePos;
            }
            
            // Update fill bar
            if (fillBar != null)
            {
                Vector3 fillScale = fillBar.localScale;
                fillScale.x = _normalizedValue;
                fillBar.localScale = fillScale;
                
                // Update fill color from gradient
                if (_fillRenderer != null && fillGradient != null)
                {
                    _fillRenderer.material.color = fillGradient.Evaluate(_normalizedValue);
                }
            }
            
            // Update value label
            if (valueLabel != null)
            {
                if (wholeNumbers)
                {
                    valueLabel.text = currentValue.ToString("F0");
                }
                else
                {
                    valueLabel.text = currentValue.ToString(valueFormat);
                }
            }
        }
        
        private void AnimateHandle(float targetScale)
        {
            if (sliderHandle == null) return;
            
            sliderHandle.DOKill();
            sliderHandle.DOScale(_handleOriginalScale * targetScale, animationDuration)
                .SetEase(animationEase);
        }
        
        private void UpdateHandleMaterial(Material material)
        {
            if (_handleRenderer != null && material != null)
            {
                _handleRenderer.material = material;
            }
        }
        
        private void TriggerHapticFeedback(float intensity)
        {
            if (_activeInteractor == null) return;
            
            // Determine which controller to vibrate
            bool isLeftHand = _activeInteractor.name.ToLower().Contains("left");
            OVRInput.Controller controller = isLeftHand ? 
                OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            
            OVRInput.SetControllerVibration(1, intensity, controller);
            DOVirtual.DelayedCall(0.1f, () => 
                OVRInput.SetControllerVibration(0, 0, controller));
        }
        
        private void PlaySnapSound()
        {
            if (audioSource != null && snapSound != null)
            {
                audioSource.PlayOneShot(snapSound, 0.7f);
            }
        }
        
        public float GetValue() => currentValue;
        public float GetNormalizedValue() => _normalizedValue;
        
        public void SetMinMax(float min, float max)
        {
            minValue = min;
            maxValue = max;
            SetValue(currentValue); // Reclamp and update
        }
        
        public void SetInteractable(bool interactable)
        {
            enabled = interactable;
            if (handleInteractable != null)
            {
                handleInteractable.enabled = interactable;
            }
            
            // Visual feedback for disabled state
            if (!interactable)
            {
                AnimateHandle(0.8f);
                // You might want to use a disabled material here
            }
        }
        
        #region Gizmos
        
        private void OnDrawGizmosSelected()
        {
            if (sliderTrack == null) return;
            
            // Draw track
            Vector3 start = sliderTrack.position - sliderTrack.right * (trackLength / 2f);
            Vector3 end = sliderTrack.position + sliderTrack.right * (trackLength / 2f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(start, end);
            
            // Draw handle position
            if (sliderHandle != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(sliderHandle.position, 0.05f);
            }
            
            // Draw notches if configured
            if (notchCount > 0)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i <= notchCount; i++)
                {
                    float t = (float)i / notchCount;
                    Vector3 notchPos = Vector3.Lerp(start, end, t);
                    Gizmos.DrawWireSphere(notchPos, 0.02f);
                }
            }
        }
        
        #endregion
    }
}
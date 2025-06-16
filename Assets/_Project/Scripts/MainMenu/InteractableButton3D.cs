using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using DG.Tweening;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Botón 3D interactuable optimizado para VR con feedback visual y háptico
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    public class InteractableButton3D : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private Transform buttonTransform;
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private GameObject hoverEffect;
        [SerializeField] private GameObject pressEffect;
        
        [Header("Materials")]
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material hoverMaterial;
        [SerializeField] private Material pressedMaterial;
        [SerializeField] private Material disabledMaterial;
        
        [Header("Animation")]
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float animationDuration = 0.15f;
        [SerializeField] private Ease scaleEase = Ease.OutBack;
        
        [Header("Button Settings")]
        [SerializeField] private string buttonLabel = "Button";
        [SerializeField] private bool isInteractable = true;
        [SerializeField] private bool playSound = true;
        
        [Header("Events")]
        public UnityEvent OnButtonPressed;
        public UnityEvent OnButtonHovered;
        public UnityEvent OnButtonUnhovered;
        
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;
        private Vector3 _originalScale;
        private Tweener _currentTween;
        private bool _isHovered = false;
        private bool _isPressed = false;
        
        // Cache de materiales para evitar instancias
        private MaterialPropertyBlock _propBlock;
        
        private void Awake()
        {
            _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            _originalScale = buttonTransform.localScale;
            _propBlock = new MaterialPropertyBlock();
            
            // Configurar texto del botón
            if (buttonText != null)
            {
                buttonText.text = buttonLabel;
            }
            
            // Ocultar efectos inicialmente
            if (hoverEffect != null) hoverEffect.SetActive(false);
            if (pressEffect != null) pressEffect.SetActive(false);
            
            // Aplicar material inicial
            UpdateButtonMaterial(normalMaterial);
        }
        
        private void OnEnable()
        {
            // Suscribirse a eventos del interactable
            _interactable.hoverEntered  .AddListener(HandleHoverEntered);
            _interactable.hoverExited.AddListener(HandleHoverExited);
            _interactable.selectEntered.AddListener(HandleSelectEntered);
            _interactable.selectExited.AddListener(HandleSelectExited);
            
            UpdateInteractability();
        }
        
        private void OnDisable()
        {
            // Desuscribirse de eventos
            _interactable.hoverEntered.RemoveListener(HandleHoverEntered);
            _interactable.hoverExited.RemoveListener(HandleHoverExited);
            _interactable.selectEntered.RemoveListener(HandleSelectEntered);
            _interactable.selectExited.RemoveListener(HandleSelectExited);
            
            // Cancelar animaciones activas
            _currentTween?.Kill();
        }
        
        #region Public Methods
        
        public void SetButtonLabel(string label)
        {
            buttonLabel = label;
            if (buttonText != null)
            {
                buttonText.text = label;
            }
        }
        
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            UpdateInteractability();
        }
        
        public void OnHoverEnter()
        {
            if (!isInteractable) return;
            
            _isHovered = true;
            
            // Animación de escala
            AnimateScale(hoverScale);
            
            // Cambiar material
            UpdateButtonMaterial(hoverMaterial);
            
            // Mostrar efecto hover
            if (hoverEffect != null)
            {
                hoverEffect.SetActive(true);
                AnimateHoverEffect();
            }
            
            OnButtonHovered?.Invoke();
        }
        
        public void OnHoverExit()
        {
            if (!isInteractable) return;
            
            _isHovered = false;
            
            if (!_isPressed)
            {
                // Restaurar escala
                AnimateScale(1f);
                
                // Restaurar material
                UpdateButtonMaterial(normalMaterial);
            }
            
            // Ocultar efecto hover
            if (hoverEffect != null)
            {
                hoverEffect.SetActive(false);
            }
            
            OnButtonUnhovered?.Invoke();
        }
        
        public void OnSelect()
        {
            if (!isInteractable) return;
            
            _isPressed = true;
            
            // Animación de presión
            AnimateScale(pressScale);
            
            // Cambiar material
            UpdateButtonMaterial(pressedMaterial);
            
            // Mostrar efecto de presión
            if (pressEffect != null)
            {
                pressEffect.SetActive(true);
                AnimatePressEffect();
            }
            
            // Ejecutar acción del botón
            OnButtonPressed?.Invoke();
            
            // Feedback adicional
            StartCoroutine(ButtonPressRoutine());
        }
        
        #endregion
        
        #region Private Methods
        
        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            // Manejado por OnHoverEnter desde SpatialUIManager
        }
        
        private void HandleHoverExited(HoverExitEventArgs args)
        {
            // Manejado por OnHoverExit desde SpatialUIManager
        }
        
        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            // Manejado por OnSelect desde SpatialUIManager
        }
        
        private void HandleSelectExited(SelectExitEventArgs args)
        {
            _isPressed = false;
            
            // Restaurar estado visual
            if (_isHovered)
            {
                AnimateScale(hoverScale);
                UpdateButtonMaterial(hoverMaterial);
            }
            else
            {
                AnimateScale(1f);
                UpdateButtonMaterial(normalMaterial);
            }
            
            // Ocultar efecto de presión
            if (pressEffect != null)
            {
                pressEffect.SetActive(false);
            }
        }
        
        private void UpdateInteractability()
        {
            _interactable.enabled = isInteractable;
            
            if (!isInteractable)
            {
                UpdateButtonMaterial(disabledMaterial);
                AnimateScale(1f);
                
                // Cambiar opacidad del texto
                if (buttonText != null)
                {
                    Color textColor = buttonText.color;
                    textColor.a = 0.5f;
                    buttonText.color = textColor;
                }
            }
            else
            {
                UpdateButtonMaterial(normalMaterial);
                
                // Restaurar opacidad del texto
                if (buttonText != null)
                {
                    Color textColor = buttonText.color;
                    textColor.a = 1f;
                    buttonText.color = textColor;
                }
            }
        }
        
        private void UpdateButtonMaterial(Material material)
        {
            if (buttonRenderer != null && material != null)
            {
                buttonRenderer.material = material;
            }
        }
        
        private void AnimateScale(float targetScale)
        {
            _currentTween?.Kill();
            
            Vector3 target = _originalScale * targetScale;
            _currentTween = buttonTransform.DOScale(target, animationDuration)
                .SetEase(scaleEase);
        }
        
        private void AnimateHoverEffect()
        {
            if (hoverEffect == null) return;
            
            // Animación de pulso para el efecto hover
            hoverEffect.transform.DOScale(_originalScale * 1.2f, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        
        private void AnimatePressEffect()
        {
            if (pressEffect == null) return;
            
            // Animación de expansión para el efecto de presión
            pressEffect.transform.localScale = Vector3.zero;
            pressEffect.transform.DOScale(Vector3.one * 1.5f, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => 
                {
                    pressEffect.SetActive(false);
                });
        }
        
        private System.Collections.IEnumerator ButtonPressRoutine()
        {
            // Esperar un frame para el feedback
            yield return new WaitForSeconds(0.1f);
            
            // Si el botón sigue presionado, mantener el estado
            if (!_isPressed)
            {
                if (_isHovered)
                {
                    AnimateScale(hoverScale);
                    UpdateButtonMaterial(hoverMaterial);
                }
                else
                {
                    AnimateScale(1f);
                    UpdateButtonMaterial(normalMaterial);
                }
            }
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmosSelected()
        {
            // Visualizar área de interacción
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, GetComponent<BoxCollider>().size);
        }
        
        #endregion
    }
}
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using TMPro;
using DG.Tweening;
using Oculus.Interaction.Surfaces;
using System.Threading;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Botón 3D interactuable optimizado para VR usando Meta Interaction SDK
    /// </summary>
    [RequireComponent(typeof(RayInteractable))]
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
        [SerializeField] private Vector3 baseScale = Vector3.one; // ESCALA BASE FIJA

        [Header("Button Settings")] 
        [SerializeField] private string buttonLabel = "Button";
        [SerializeField] private bool isInteractable = true;
        [SerializeField] private bool playSound = true;

        [Header("Interaction Control")] 
        [SerializeField] private bool allowRepeatOnHold = false;
        [SerializeField] private float repeatDelay = 0.5f;
        [SerializeField] private float repeatInterval = 0.1f;
        [SerializeField] private bool requireFullRelease = true;

        [Header("Ray Interaction")] 
        [SerializeField] private ColliderSurface buttonSurface;
        [SerializeField] private float surfaceRadius = 0.1f;

        [Header("Events")] 
        public UnityEvent OnButtonPressed;
        public UnityEvent OnButtonHovered;
        public UnityEvent OnButtonUnhovered;
        public UnityEvent OnButtonHeld;

        // Private fields
        private RayInteractable _rayInteractable;
        private Tweener _currentTween;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _canTrigger = true;
        private float _lastTriggerTime = 0f;
        
        // UniTask cancellation tokens
        private CancellationTokenSource _repeatCts;
        
        // Tracking de interactores
        private Dictionary<RayInteractor, InteractorState> _interactorStates = new Dictionary<RayInteractor, InteractorState>();
        private RayInteractor _activeInteractor;

        public bool CanDebug = false;

        private void Awake()
        {
            if (CanDebug)
            {
                Debug.Log($"[{name}] Awake - Base scale: {baseScale}");
            }

            _rayInteractable = GetComponent<RayInteractable>();
            if (_rayInteractable == null)
            {
                _rayInteractable = gameObject.AddComponent<RayInteractable>();
            }

            if (buttonSurface == null)
            {
                CreateDefaultSurface();
            }

            if (buttonText != null)
            {
                buttonText.text = buttonLabel;
            }

            if (hoverEffect != null) hoverEffect.SetActive(false);
            if (pressEffect != null) pressEffect.SetActive(false);

            UpdateButtonMaterial(normalMaterial);
        }

        private void CreateDefaultSurface()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(1f, 0.2f, 0.1f);
                collider.isTrigger = true;
            }

            buttonSurface = gameObject.AddComponent<ColliderSurface>();
        }

        private void OnEnable()
        {
            if (CanDebug)
            {
                Debug.Log($"[{name}] OnEnable");
            }
            
            if (_rayInteractable != null)
            {
                _rayInteractable.InjectSurface(buttonSurface);
            }

            // Siempre restaurar a la escala base
            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            targetTransform.localScale = baseScale;

            UpdateInteractability();
            _canTrigger = true;
            _interactorStates.Clear();
            
            // Resetear estados visuales
            _isHovered = false;
            _isPressed = false;
            _activeInteractor = null;
            
            // Asegurar material correcto
            UpdateButtonMaterial(isInteractable ? normalMaterial : disabledMaterial);
        }

        private void OnDisable()
        {
            if (CanDebug)
            {
                Debug.Log($"[{name}] OnDisable");
            }

            // Cancelar todas las tareas async
            _repeatCts?.Cancel();
            
            // Detener y limpiar animaciones
            _currentTween?.Kill(true);
            _currentTween = null;

            // Restaurar escala base
            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            targetTransform.localScale = baseScale;

            // Resetear estados
            _isHovered = false;
            _isPressed = false;
            _canTrigger = true;
            _activeInteractor = null;
            _interactorStates.Clear();
            
            // Detener efectos visuales
            if (hoverEffect != null) 
            {
                hoverEffect.transform.DOKill();
                hoverEffect.SetActive(false);
            }
            
            if (pressEffect != null) 
            {
                pressEffect.transform.DOKill();
                pressEffect.SetActive(false);
            }
        }

        private void Update()
        {
            CheckForInteractions();
        }

        private void CheckForInteractions()
        {
            if (!isInteractable) return;

            var rayInteractors = FindObjectsOfType<RayInteractor>();

            foreach (var interactor in rayInteractors)
            {
                bool isHoveringThis = false;

                if (interactor.HasCandidate && 
                    interactor.CandidateProperties is RayInteractor.RayCandidateProperties props && 
                    props.ClosestInteractable == _rayInteractable)
                {
                    isHoveringThis = true;
                }

                UpdateInteractorState(interactor, isHoveringThis);
            }
        }

        private void UpdateInteractorState(RayInteractor interactor, bool isHovering)
        {
            InteractorState previousState = InteractorState.Normal;
            if (_interactorStates.ContainsKey(interactor))
            {
                previousState = _interactorStates[interactor];
            }

            InteractorState currentState = interactor.State;
            _interactorStates[interactor] = currentState;

            if (isHovering)
            {
                if (!_isHovered && _activeInteractor == null)
                {
                    _activeInteractor = interactor;
                    OnHoverEnter();
                }

                if (currentState == InteractorState.Select && previousState != InteractorState.Select)
                {
                    if (_canTrigger || !requireFullRelease)
                    {
                        OnSelectStart();
                    }
                }
                else if (currentState != InteractorState.Select && previousState == InteractorState.Select)
                {
                    OnSelectEnd();
                }
            }
            else if (_activeInteractor == interactor)
            {
                OnHoverExit();
                _activeInteractor = null;

                if (_isPressed)
                {
                    OnSelectEnd();
                }
            }
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

        public void SetAllowRepeatOnHold(bool allow)
        {
            allowRepeatOnHold = allow;
            if (!allow)
            {
                _repeatCts?.Cancel();
            }
        }

        public void SetRepeatParameters(float delay, float interval)
        {
            repeatDelay = Mathf.Max(0.1f, delay);
            repeatInterval = Mathf.Max(0.05f, interval);
        }

        public void SetBaseScale(Vector3 scale)
        {
            baseScale = scale;
            if (!_isHovered && !_isPressed)
            {
                Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
                targetTransform.localScale = baseScale;
            }
        }

        #endregion

        #region Interaction Handlers

        public void OnHoverEnter()
        {
            if (!isInteractable || _isHovered) return;

            if (CanDebug)
            {
                Debug.Log($"[{name}] OnHoverEnter");
            }

            _isHovered = true;

            AnimateScale(hoverScale);
            UpdateButtonMaterial(hoverMaterial);

            if (hoverEffect != null)
            {
                hoverEffect.SetActive(true);
                AnimateHoverEffect();
            }

            OnButtonHovered?.Invoke();
        }

        public void OnHoverExit()
        {
            if (!isInteractable || !_isHovered) return;

            if (CanDebug)
            {
                Debug.Log($"[{name}] OnHoverExit");
            }

            _isHovered = false;

            if (!_isPressed)
            {
                AnimateScale(1f);
                UpdateButtonMaterial(normalMaterial);
            }

            if (hoverEffect != null)
            {
                hoverEffect.transform.DOKill();
                hoverEffect.SetActive(false);
            }

            OnButtonUnhovered?.Invoke();
        }

        public async void OnSelectStart()
        {
            if (!isInteractable || _isPressed) return;

            if (CanDebug)
            {
                Debug.Log($"[{name}] OnSelectStart");
            }
            
            // Verificar si este botón NO es parte del teclado virtual
            // Si no es parte del teclado, cerrar el teclado si está abierto
            VirtualKeyboard3D parentKeyboard = GetComponentInParent<VirtualKeyboard3D>();
            if (parentKeyboard == null)
            {
                // Este botón no es parte de un teclado virtual
                // Verificar si hay un teclado abierto y cerrarlo
                VirtualKeyboardManager keyboardManager = VirtualKeyboardManager.Instance;
                if (keyboardManager != null && keyboardManager.IsKeyboardVisible)
                {
                    if (CanDebug)
                    {
                        Debug.Log($"[{name}] Button pressed outside virtual keyboard, closing keyboard");
                    }
                    keyboardManager.HideKeyboard();
                }
            }

            _isPressed = true;
            _canTrigger = false; 

            AnimateScale(pressScale);
            UpdateButtonMaterial(pressedMaterial);

            if (pressEffect != null)
            {
                pressEffect.SetActive(true);
                AnimatePressEffect();
            }

            ExecuteButtonAction();

            if (allowRepeatOnHold)
            {
                _repeatCts?.Cancel();
                _repeatCts = new CancellationTokenSource();
                RepeatActionAsync(_repeatCts.Token).Forget();
            }
        }

        public void OnSelectEnd()
        {
            if (!_isPressed) return;

            if (CanDebug)
            {
                Debug.Log($"[{name}] OnSelectEnd");
            }

            _isPressed = false;
            _repeatCts?.Cancel();

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

            if (requireFullRelease)
            {
                _canTrigger = true;
            }
        }

        #endregion

        #region Private Methods

        private void ExecuteButtonAction()
        {
            _lastTriggerTime = Time.time;
            OnButtonPressed?.Invoke();
        }

        private async UniTaskVoid RepeatActionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay((int)(repeatDelay * 1000), cancellationToken: cancellationToken);

                while (_isPressed && allowRepeatOnHold && !cancellationToken.IsCancellationRequested)
                {
                    OnButtonHeld?.Invoke();
                    ExecuteButtonAction();
                    await UniTask.Delay((int)(repeatInterval * 1000), cancellationToken: cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Cancelled, es normal
            }
        }

        private void UpdateInteractability()
        {
            if (_rayInteractable != null)
            {
                _rayInteractable.enabled = isInteractable;
            }

            if (!isInteractable)
            {
                _currentTween?.Kill(true);
                
                UpdateButtonMaterial(disabledMaterial);
                AnimateScale(1f);

                if (buttonText != null)
                {
                    Color textColor = buttonText.color;
                    textColor.a = 0.5f;
                    buttonText.color = textColor;
                }

                _repeatCts?.Cancel();
            }
            else
            {
                UpdateButtonMaterial(normalMaterial);

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

            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            Vector3 target = baseScale * targetScale; // Usar baseScale en lugar de una variable dinámica

            _currentTween = targetTransform.DOScale(target, animationDuration)
                .SetEase(scaleEase)
                .OnKill(() => _currentTween = null);
        }

        private void AnimateHoverEffect()
        {
            if (hoverEffect == null) return;

            hoverEffect.transform.DOKill();
            
            hoverEffect.transform.localScale = Vector3.one;
            hoverEffect.transform.DOScale(Vector3.one * 1.2f, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void AnimatePressEffect()
        {
            if (pressEffect == null) return;

            pressEffect.transform.DOKill();
            
            pressEffect.transform.localScale = Vector3.zero;
            pressEffect.transform.DOScale(Vector3.one * 1.5f, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => { 
                    if (pressEffect != null) 
                        pressEffect.SetActive(false); 
                });
        }

        #endregion

        private void OnDestroy()
        {
            // Cancelar todas las tareas async
            _repeatCts?.Cancel();
            
            // Limpiar todas las animaciones
            _currentTween?.Kill();
            
            if (hoverEffect != null)
                hoverEffect.transform.DOKill();
                
            if (pressEffect != null)
                pressEffect.transform.DOKill();
            
            _interactorStates.Clear();
        }
        
        #if UNITY_EDITOR
        // Helper para resetear la escala base en el editor
        [ContextMenu("Reset Base Scale to Current")]
        private void ResetBaseScaleTourrent()
        {
            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            baseScale = targetTransform.localScale;
            Debug.Log($"Base scale set to: {baseScale}");
        }
        
        [ContextMenu("Apply Base Scale")]
        private void ApplyBaseScale()
        {
            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            targetTransform.localScale = baseScale;
        }
        #endif
    }
}
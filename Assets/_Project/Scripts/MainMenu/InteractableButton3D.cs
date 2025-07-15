using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using TMPro;
using DG.Tweening;
using Oculus.Interaction.Surfaces;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Botón 3D interactuable optimizado para VR usando Meta Interaction SDK
    /// </summary>
    [RequireComponent(typeof(RayInteractable))]
    public class InteractableButton3D : MonoBehaviour
    {
        [Header("Visual Components")] [SerializeField]
        private Transform buttonTransform;

        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private GameObject hoverEffect;
        [SerializeField] private GameObject pressEffect;

        [Header("Materials")] [SerializeField] private Material normalMaterial;
        [SerializeField] private Material hoverMaterial;
        [SerializeField] private Material pressedMaterial;
        [SerializeField] private Material disabledMaterial;

        [Header("Animation")] [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float animationDuration = 0.15f;
        [SerializeField] private Ease scaleEase = Ease.OutBack;

        [Header("Button Settings")] [SerializeField]
        private string buttonLabel = "Button";

        [SerializeField] private bool isInteractable = true;
        [SerializeField] private bool playSound = true;

        [Header("Interaction Control")] [SerializeField]
        private bool allowRepeatOnHold = false;

        [SerializeField] private float repeatDelay = 0.5f;
        [SerializeField] private float repeatInterval = 0.1f;
        [SerializeField] private bool requireFullRelease = true;

        [Header("Ray Interaction")] [SerializeField]
        private ColliderSurface buttonSurface;

        [SerializeField] private float surfaceRadius = 0.1f;

        [Header("Events")] public UnityEvent OnButtonPressed;
        public UnityEvent OnButtonHovered;
        public UnityEvent OnButtonUnhovered;
        public UnityEvent OnButtonHeld;

        private RayInteractable _rayInteractable;
        private Vector3 _originalScale;
        private Tweener _currentTween;
        private bool _isHovered = false;
        [SerializeField] private bool _isPressed = false;
        private bool _canTrigger = true;
        private float _lastTriggerTime = 0f;
        private Coroutine _repeatCoroutine;

        // Tracking de interactores para control preciso
        private Dictionary<RayInteractor, InteractorState> _interactorStates =
            new Dictionary<RayInteractor, InteractorState>();

        private RayInteractor _activeInteractor;

        private MaterialPropertyBlock _propBlock;

        public bool CanDebug = false;
        private bool _scaleInitialize;

        private void Awake()
        {
            if (CanDebug)
            {
                Debug.Log(this.name);
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

            _originalScale = buttonTransform != null ? buttonTransform.localScale : transform.localScale;
            _scaleInitialize = true;
            _propBlock = new MaterialPropertyBlock();

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
            if (_rayInteractable != null)
            {
                _rayInteractable.InjectSurface(buttonSurface);
            }

            UpdateInteractability();
            _canTrigger = true;
            _interactorStates.Clear();

            _originalScale = buttonTransform != null ? buttonTransform.localScale : transform.localScale;
            _scaleInitialize = true;
        }

        private void OnDisable()
        {
            _currentTween?.Kill();

            if (_repeatCoroutine != null)
            {
                StopCoroutine(_repeatCoroutine);
                _repeatCoroutine = null;
            }

            _isHovered = false;
            _isPressed = false;
            _canTrigger = true;
            _activeInteractor = null;
            _interactorStates.Clear();
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

                if (interactor.HasCandidate && interactor.CandidateProperties is RayInteractor.RayCandidateProperties props && props.ClosestInteractable == _rayInteractable)
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
            if (!allow && _repeatCoroutine != null)
            {
                StopCoroutine(_repeatCoroutine);
                _repeatCoroutine = null;
            }
        }

        public void SetRepeatParameters(float delay, float interval)
        {
            repeatDelay = Mathf.Max(0.1f, delay);
            repeatInterval = Mathf.Max(0.05f, interval);
        }

        #endregion

        #region Interaction Handlers

        public void OnHoverEnter()
        {
            if (!isInteractable || _isHovered) return;

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

            _isHovered = false;

            if (!_isPressed)
            {
                AnimateScale(1f);

                UpdateButtonMaterial(normalMaterial);
            }

            if (hoverEffect != null)
            {
                hoverEffect.SetActive(false);
            }

            OnButtonUnhovered?.Invoke();
        }

        public void OnSelectStart()
        {
            if (!isInteractable || _isPressed) return;

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
                _repeatCoroutine = StartCoroutine(RepeatActionCoroutine());
            }
        }

        private void OnSelectEnd()
        {
            if (!_isPressed) return;

            _isPressed = false;

            if (_repeatCoroutine != null)
            {
                StopCoroutine(_repeatCoroutine);
                _repeatCoroutine = null;
            }

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

        /*public async void OnSelect()
        {
            if (!isInteractable) return;

            // Si no hay un interactor activo, simular una pulsación completa
            if (_activeInteractor == null)
            {
                OnSelectStart();
                await UniTask.Delay(100);
                OnSelectEnd();
            }
        }*/

        #endregion

        #region Private Methods

        private void ExecuteButtonAction()
        {
            _lastTriggerTime = Time.time;
            OnButtonPressed?.Invoke();

            ButtonPressRoutine().Forget();
        }

        private IEnumerator RepeatActionCoroutine()
        {
            yield return new WaitForSeconds(repeatDelay);

            while (_isPressed && allowRepeatOnHold)
            {
                OnButtonHeld?.Invoke();
                ExecuteButtonAction();
                yield return new WaitForSeconds(repeatInterval);
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
                UpdateButtonMaterial(disabledMaterial);
                AnimateScale(1f);

                if (buttonText != null)
                {
                    Color textColor = buttonText.color;
                    textColor.a = 0.5f;
                    buttonText.color = textColor;
                }

                if (_repeatCoroutine != null)
                {
                    StopCoroutine(_repeatCoroutine);
                    _repeatCoroutine = null;
                }
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
            if (!_scaleInitialize)
            {
                if (gameObject.activeInHierarchy)
                {
                    _originalScale = transform.localScale;
                    _scaleInitialize = true;
                }
                else
                {
                    Debug.LogError($"❌[{name}] this can not be animated, because its not active");
                    return;
                }
            }

            _currentTween?.Kill();

            Transform targetTransform = buttonTransform != null ? buttonTransform : transform;
            Vector3 target = _originalScale * targetScale;

            _currentTween = targetTransform.DOScale(target, animationDuration).SetEase(scaleEase);
        }

        private void AnimateHoverEffect()
        {
            if (hoverEffect == null) return;

            hoverEffect.transform.DOScale(_originalScale * 1.2f, 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void AnimatePressEffect()
        {
            if (pressEffect == null) return;

            pressEffect.transform.localScale = Vector3.zero;
            pressEffect.transform.DOScale(Vector3.one * 1.5f, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => { pressEffect.SetActive(false); });
        }

        private async UniTask ButtonPressRoutine()
        {
            await UniTask.Delay(100);
        }

        #endregion

        #region Gizmos

        //private void OnDrawGizmosSelected()
        //{
        //    // Visualizar área de interacción
        //    Gizmos.color = Color.green;

        //    BoxCollider boxCollider = GetComponent<BoxCollider>();
        //    if (boxCollider != null)
        //    {
        //        Gizmos.DrawWireCube(transform.position + boxCollider.center, boxCollider.size);
        //    }

        //    // Visualizar radio de superficie
        //    Gizmos.color = Color.cyan;
        //    Gizmos.DrawWireSphere(transform.position, surfaceRadius);
        //}

        #endregion

        private void OnDestroy()
        {
            _currentTween?.Kill();
            if (_repeatCoroutine != null)
            {
                StopCoroutine(_repeatCoroutine);
            }

            _interactorStates.Clear();
        }
    }
}
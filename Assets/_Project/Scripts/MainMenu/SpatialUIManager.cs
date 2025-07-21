using UnityEngine;
using Oculus.Interaction;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Gestiona el sistema de UI espacial 3D para VR usando Meta Interaction SDK
    /// </summary>
    public class SpatialUIManager : MonoBehaviour
    {
        [Header("UI Configuration")]
        [SerializeField] private float defaultPanelDistance = 2f;
        [SerializeField] private float panelHeight = 1.5f;
        [SerializeField] private float panelSpacing = 0.8f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Meta Ray Interactors")]
        [SerializeField] private RayInteractor leftHandRayInteractor;
        [SerializeField] private RayInteractor rightHandRayInteractor;
        
        [Header("Visual Feedback")]
        [SerializeField] private LineRenderer leftRayVisual;
        [SerializeField] private LineRenderer rightRayVisual;
        [SerializeField] private GameObject reticle;
        [SerializeField] private float rayWidth = 0.01f;
        [SerializeField] private Gradient rayGradient;
        [SerializeField] private Gradient rayHoverGradient;

        [Header("Haptic Feedback")]
        [SerializeField] private float hoverHapticStrength = 0.1f;
        [SerializeField] private float selectHapticStrength = 0.3f;
        [SerializeField] private float hapticDuration = 0.1f;

        [Header("Audio")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip backSound;

        private Dictionary<PanelID, MenuPanel> _panels = new Dictionary<PanelID, MenuPanel>();
        private MenuPanel _currentPanel;
        private Stack<MenuPanel> _navigationStack = new Stack<MenuPanel>();
        private Transform _playerTransform;
        private bool _isTransitioning = false;
        
        // Ray interaction tracking
        private RayInteractable _currentHoveredInteractable;
        private Dictionary<RayInteractor, RayInteractable> _hoveredInteractables = new Dictionary<RayInteractor, RayInteractable>();
        private Dictionary<RayInteractor, InteractorState> _previousInteractorStates = new Dictionary<RayInteractor, InteractorState>();


        public static SpatialUIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ConfigureRayInteractors();
        }

        private void Start()
        {
            _playerTransform = Camera.main.transform.parent;

            RegisterChildPanels();

            SetupRayVisuals();

            ShowPanel(PanelID.MainPanel);
        }

        private void Update()
        {
            UpdateRayVisuals();
            CheckForInteractions();
        }

        private void ConfigureRayInteractors()
        {
            if (leftHandRayInteractor != null)
            {
                leftHandRayInteractor.MaxRayLength = 10f;
            }

            if (rightHandRayInteractor != null)
            {
                rightHandRayInteractor.MaxRayLength = 10f;
            }
        }

        private void SetupRayVisuals()
        {
            if (leftRayVisual != null)
            {
                leftRayVisual.startWidth = rayWidth;
                leftRayVisual.endWidth = rayWidth * 0.5f;
                leftRayVisual.colorGradient = rayGradient;
                leftRayVisual.positionCount = 2;
            }

            if (rightRayVisual != null)
            {
                rightRayVisual.startWidth = rayWidth;
                rightRayVisual.endWidth = rayWidth * 0.5f;
                rightRayVisual.colorGradient = rayGradient;
                rightRayVisual.positionCount = 2;
            }

            if (reticle == null)
            {
                reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reticle.transform.localScale = Vector3.one * 0.02f;
                reticle.GetComponent<Renderer>().material.color = Color.white;
                Destroy(reticle.GetComponent<Collider>());
            }
        }

        private void UpdateRayVisuals()
        {
            UpdateSingleRayVisual(leftHandRayInteractor, leftRayVisual, true);
            
            UpdateSingleRayVisual(rightHandRayInteractor, rightRayVisual, false);
        }

        private void UpdateSingleRayVisual(RayInteractor rayInteractor, LineRenderer rayVisual, bool isLeft)
        {
            if (rayInteractor == null || rayVisual == null) return;

            bool isInteracting = rayInteractor.State == InteractorState.Select;
            bool hasCandidate = rayInteractor.HasCandidate;
            
            rayVisual.enabled = rayInteractor.enabled && (hasCandidate || isInteracting);

            if (rayVisual.enabled)
            {
                rayVisual.SetPosition(0, rayInteractor.Origin);
                rayVisual.SetPosition(1, rayInteractor.End);

                if (hasCandidate)
                {
                    rayVisual.colorGradient = rayHoverGradient;
                    
                    if (rayInteractor.CollisionInfo.HasValue && reticle != null)
                    {
                        reticle.SetActive(true);
                        reticle.transform.position = rayInteractor.CollisionInfo.Value.Point;
                        reticle.transform.rotation = Quaternion.LookRotation(rayInteractor.CollisionInfo.Value.Normal);
                    }
                }
                else
                {
                    rayVisual.colorGradient = rayGradient;
                    if (reticle != null) reticle.SetActive(false);
                }
            }
            else
            {
                if (reticle != null) reticle.SetActive(false);
            }
        }

        private void CheckForInteractions()
        {
            CheckHandInteraction(leftHandRayInteractor);
            CheckHandInteraction(rightHandRayInteractor);
        }

        private void CheckHandInteraction(RayInteractor rayInteractor)
        {
            if (rayInteractor == null) return;

            RayInteractable currentInteractable = null;
            if (rayInteractor.HasCandidate && rayInteractor.CandidateProperties is RayInteractor.RayCandidateProperties props)
            {
                currentInteractable = props.ClosestInteractable;
            }

            if (_hoveredInteractables.TryGetValue(rayInteractor, out RayInteractable previousInteractable))
            {
                if (previousInteractable != currentInteractable)
                {
                    if (previousInteractable != null)
                    {
                        OnRayHoverExit(previousInteractable);
                    }

                    if (currentInteractable != null)
                    {
                        OnRayHoverEnter(currentInteractable, rayInteractor);
                    }
                }
            }
            else if (currentInteractable != null)
            {
                OnRayHoverEnter(currentInteractable, rayInteractor);
            }

            _hoveredInteractables[rayInteractor] = currentInteractable;

            InteractorState previousState = _previousInteractorStates.ContainsKey(rayInteractor) 
                ? _previousInteractorStates[rayInteractor] 
                : InteractorState.Normal;
            InteractorState currentState = rayInteractor.State;

            if (currentState == InteractorState.Select && 
                previousState != InteractorState.Select && 
                currentInteractable != null)
            {
                OnRaySelect(currentInteractable, rayInteractor);
            }

            _previousInteractorStates[rayInteractor] = currentState;
        }

        private void OnRayHoverEnter(RayInteractable interactable, RayInteractor interactor)
        {
            var button = interactable.GetComponentInParent<InteractableButton3D>();
            if (button != null)
            {
                button.OnHoverEnter();
        
                VirtualKeyboard3D keyboard = button.GetComponentInParent<VirtualKeyboard3D>();
        
                if (keyboard == null)
                {
                    TriggerHapticFeedback(interactor, hoverHapticStrength);
                    PlayUISound(hoverSound, 0.5f);
                }
            }
        }

        private void OnRayHoverExit(RayInteractable interactable)
        {
            var button = interactable.GetComponentInParent<InteractableButton3D>();
            if (button != null)
            {
                button.OnHoverExit();
            }
        }

        private void OnRaySelect(RayInteractable interactable, RayInteractor interactor)
        {
            var button = interactable.GetComponentInParent<InteractableButton3D>();
            if (button != null)
            {
                button.OnSelectStart();
                TriggerHapticFeedback(interactor, selectHapticStrength);
                VirtualKeyboard3D keyboard = button.GetComponentInParent<VirtualKeyboard3D>();
                if (keyboard == null)
                {
                    PlayUISound(selectSound);
                }
            }
        }

        private void RegisterChildPanels()
        {
            MenuPanel[] panels = GetComponentsInChildren<MenuPanel>(true);
            foreach (var panel in panels)
            {
                RegisterPanel(panel.panelID, panel);
                panel.gameObject.SetActive(false);
            }
        }

        public void RegisterPanel(PanelID panelId, MenuPanel panel)
        {
            if (!_panels.ContainsKey(panelId))
            {
                _panels.Add(panelId, panel);
                panel.Initialize(this);
            }
        }

        public void ShowPanel(PanelID panelId, bool addToStack = true)
        {
            if (_isTransitioning || !_panels.ContainsKey(panelId)) return;

            StartCoroutine(TransitionToPanel(panelId, addToStack));
        }

        private System.Collections.IEnumerator TransitionToPanel(PanelID panelId, bool addToStack)
        {
            _isTransitioning = true;
            MenuPanel targetPanel = _panels[panelId];

            if (_currentPanel != null)
            {
                yield return StartCoroutine(AnimatePanelOut(_currentPanel));
                _currentPanel.gameObject.SetActive(false);

                if (addToStack)
                {
                    _navigationStack.Push(_currentPanel);
                }
            }

            PositionPanelInFrontOfPlayer(targetPanel);

            targetPanel.gameObject.SetActive(true);
            yield return StartCoroutine(AnimatePanelIn(targetPanel));

            _currentPanel = targetPanel;
            _currentPanel.OnPanelShown();

            PlayUISound(selectSound);

            _isTransitioning = false;
        }

        private void PositionPanelInFrontOfPlayer(MenuPanel panel)
        {
            if (_playerTransform == null) return;

            Vector3 forward = _playerTransform.forward;
            forward.y = 0; 
            forward.Normalize();

            Vector3 targetPosition = _playerTransform.position + forward * defaultPanelDistance;
            targetPosition.y = panelHeight;

            panel.transform.position = targetPosition;

            Vector3 lookDirection = _playerTransform.position - panel.transform.position;
            lookDirection.y = 0;
            panel.transform.rotation = Quaternion.LookRotation(-lookDirection);
        }

        public void GoBack()
        {
            if (_isTransitioning || _navigationStack.Count == 0) return;

            MenuPanel previousPanel = _navigationStack.Pop();
            ShowPanel(previousPanel.panelID, false);
            PlayUISound(backSound);
        }

        private System.Collections.IEnumerator AnimatePanelIn(MenuPanel panel)
        {
            float duration = 0.3f;
            float elapsed = 0f;

            panel.CanvasGroup.alpha = 0f;
            panel.transform.localScale = Vector3.one * 0.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curveValue = scaleCurve.Evaluate(t);

                panel.CanvasGroup.alpha = curveValue;
                panel.transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, curveValue);

                yield return null;
            }

            panel.CanvasGroup.alpha = 1f;
            panel.transform.localScale = Vector3.one;
        }

        private System.Collections.IEnumerator AnimatePanelOut(MenuPanel panel)
        {
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curveValue = 1f - scaleCurve.Evaluate(t);

                panel.CanvasGroup.alpha = curveValue;
                panel.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, t);

                yield return null;
            }

            panel.CanvasGroup.alpha = 0f;
        }

        #region Haptic Feedback

        private void TriggerHapticFeedback(RayInteractor interactor, float intensity)
        {
            if (interactor == leftHandRayInteractor)
            {
                OVRInput.SetControllerVibration(1, intensity, OVRInput.Controller.LTouch);
                DOVirtual.DelayedCall(hapticDuration, () => 
                    OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch));
            }
            else if (interactor == rightHandRayInteractor)
            {
                OVRInput.SetControllerVibration(1, intensity, OVRInput.Controller.RTouch);
                DOVirtual.DelayedCall(hapticDuration, () => 
                    OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch));
            }
        }

        #endregion

        #region Audio

        private void PlayUISound(AudioClip clip, float volume = 1f)
        {
            if (uiAudioSource != null && clip != null)
            {
                uiAudioSource.PlayOneShot(clip, volume);
            }
        }

        #endregion

        #region Panel Arrangement

        public void ArrangePanelsInArc(List<MenuPanel> panels, float arcAngle = 60f)
        {
            if (panels == null || panels.Count == 0) return;

            float angleStep = arcAngle / (panels.Count - 1);
            float startAngle = -arcAngle / 2f;

            for (int i = 0; i < panels.Count; i++)
            {
                float angle = startAngle + (angleStep * i);
                float radians = angle * Mathf.Deg2Rad;

                Vector3 position = new Vector3(
                    Mathf.Sin(radians) * defaultPanelDistance,
                    panelHeight,
                    Mathf.Cos(radians) * defaultPanelDistance
                );

                panels[i].transform.position = _playerTransform.position + position;
                panels[i].transform.LookAt(_playerTransform.position);
                panels[i].transform.rotation *= Quaternion.Euler(0, 180, 0);
            }
        }

        #endregion

        private void OnDestroy()
        {
            _hoveredInteractables.Clear();
            _previousInteractorStates.Clear();

        }
    }

    public enum PanelID
    {
        MainPanel,
        LobbyBrowser,
        CreateLobby,
        Lobby,
        LobbyRoom,
        Friends,
        Settings,
        Options,
        ExitPanel,
        Results
    }
}
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HackMonkeys.UI.Spatial
{
    /// <summary>
    /// Gestiona el sistema de UI espacial 3D para VR con optimizaciones para Quest 2
    /// </summary>
    public class SpatialUIManager : MonoBehaviour
    {
        [Header("UI Configuration")] [SerializeField]
        private float defaultPanelDistance = 2f;

        [SerializeField] private float panelHeight = 1.5f;
        [SerializeField] private float panelSpacing = 0.8f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Interaction")] [SerializeField]
        private XRRayInteractor leftHandRay;

        [SerializeField] private XRRayInteractor rightHandRay;
        [SerializeField] private LineRenderer rayLineRenderer;
        [SerializeField] private float rayMaxDistance = 10f;

        [Header("Haptic Feedback")] [SerializeField]
        private float hoverHapticStrength = 0.1f;

        [SerializeField] private float selectHapticStrength = 0.3f;
        [SerializeField] private float hapticDuration = 0.1f;

        [Header("Audio")] [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip backSound;

        private Dictionary<PanelID, MenuPanel> _panels = new Dictionary<PanelID, MenuPanel>();
        private MenuPanel _currentPanel;
        private Stack<MenuPanel> _navigationStack = new Stack<MenuPanel>();
        private Transform _playerTransform;
        private bool _isTransitioning = false;

        public static SpatialUIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Configurar ray interactors
            ConfigureRayInteractors();
        }

        private void Start()
        {
            // Obtener referencia al transform del jugador VR
            _playerTransform = Camera.main.transform.parent;

            // Registrar todos los paneles hijos
            RegisterChildPanels();

            // Mostrar panel principal
            ShowPanel(PanelID.MainPanel);
        }

        private void ConfigureRayInteractors()
        {
            // Configurar ray de mano izquierda
            if (leftHandRay != null)
            {
                leftHandRay.maxRaycastDistance = rayMaxDistance;
                leftHandRay.hoverEntered.AddListener(OnHoverEnter);
                leftHandRay.hoverExited.AddListener(OnHoverExit);
                leftHandRay.selectEntered.AddListener(OnSelect);
            }

            // Configurar ray de mano derecha
            if (rightHandRay != null)
            {
                rightHandRay.maxRaycastDistance = rayMaxDistance;
                rightHandRay.hoverEntered.AddListener(OnHoverEnter);
                rightHandRay.hoverExited.AddListener(OnHoverExit);
                rightHandRay.selectEntered.AddListener(OnSelect);
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

            // Ocultar panel actual
            if (_currentPanel != null)
            {
                yield return StartCoroutine(AnimatePanelOut(_currentPanel));
                _currentPanel.gameObject.SetActive(false);

                if (addToStack)
                {
                    _navigationStack.Push(_currentPanel);
                }
            }

            // Posicionar nuevo panel frente al jugador
            PositionPanelInFrontOfPlayer(targetPanel);

            // Mostrar nuevo panel
            targetPanel.gameObject.SetActive(true);
            yield return StartCoroutine(AnimatePanelIn(targetPanel));

            _currentPanel = targetPanel;
            _currentPanel.OnPanelShown();

            // Reproducir sonido de transición
            PlayUISound(selectSound);

            _isTransitioning = false;
        }

        private void PositionPanelInFrontOfPlayer(MenuPanel panel)
        {
            if (_playerTransform == null) return;

            // Calcular posición frente al jugador
            Vector3 forward = _playerTransform.forward;
            forward.y = 0; // Mantener panel a nivel horizontal
            forward.Normalize();

            Vector3 targetPosition = _playerTransform.position + forward * defaultPanelDistance;
            targetPosition.y = panelHeight;

            panel.transform.position = targetPosition;

            // Rotar panel para que mire al jugador
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

        #region Interaction Callbacks

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            var button = args.interactableObject.transform.GetComponent<InteractableButton3D>();
            if (button != null)
            {
                button.OnHoverEnter();
                TriggerHapticFeedback(args.interactorObject, hoverHapticStrength);
                PlayUISound(hoverSound, 0.5f);
            }
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            var button = args.interactableObject.transform.GetComponent<InteractableButton3D>();
            if (button != null)
            {
                button.OnHoverExit();
            }
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            var button = args.interactableObject.transform.GetComponent<InteractableButton3D>();
            if (button != null)
            {
                button.OnSelect();
                TriggerHapticFeedback(args.interactorObject, selectHapticStrength);
                PlayUISound(selectSound);
            }
        }

        #endregion

        #region Haptic Feedback

        private void TriggerHapticFeedback(IXRInteractor interactor, float intensity)
        {
            if (interactor is XRBaseInputInteractor controllerInteractor)
            {
                var controller = controllerInteractor.xrController;
                if (controller != null)
                {
                    controller.SendHapticImpulse(intensity, hapticDuration);
                }
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
    }

    public enum PanelID
    {
        MainPanel,
        LobbyBrowser,
        CreateRoom,
        Friends,
        Settings
    }
}
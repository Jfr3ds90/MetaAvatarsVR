using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;
using System;
using UnityEngine.XR.Interaction.Toolkit;
using HackMonkeys.Debugging;

namespace MetaAvatarsVR.Networking
{
    /// <summary>
    /// Componente para manejar el botón de cambio de modo en la linterna
    /// Compatible con PokeInteractable de Meta XR SDK
    /// </summary>
    [RequireComponent(typeof(PokeInteractable))]
    public class FlashlightButton : MonoBehaviour
    {
        #region Events
        public event Action OnButtonPressed;
        public event Action OnButtonReleased;
        public UnityEvent OnPressed = new UnityEvent();
        public UnityEvent OnReleased = new UnityEvent();
        #endregion

        #region Serialized Fields
        [Header("Button Configuration")]
        [SerializeField] private Transform buttonTransform;
        [SerializeField] private float pressDepth = 0.01f;
        [SerializeField] private float pressSpeed = 10f;

        [Header("Visual Feedback")]
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material pressedMaterial;
        [SerializeField] private Material hoveredMaterial;

        [Header("Haptic Feedback")]
        [SerializeField] private float hapticAmplitude = 0.5f;
        [SerializeField] private float hapticDuration = 0.1f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pressSound;
        [SerializeField] private AudioClip releaseSound;

        #endregion

        #region Private Fields
        private PokeInteractable pokeInteractable;
        private Vector3 originalPosition;
        private Vector3 pressedPosition;
        private bool isPressed = false;
        private bool isHovered = false;

        // Estados internos del botón
        private enum ButtonState
        {
            Normal,
            Hover,
            Select
        }
        private ButtonState currentState = ButtonState.Normal;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            pokeInteractable = GetComponent<PokeInteractable>();

            if (buttonTransform == null)
            {
                buttonTransform = transform;
            }

            originalPosition = buttonTransform.localPosition;
            pressedPosition = originalPosition - (buttonTransform.forward * pressDepth);

            SetupInteractable();
            ValidateComponents();
        }

        private void Start()
        {
            // Aplicar material inicial
            UpdateButtonVisual(ButtonState.Normal);
        }

        private void OnEnable()
        {
            if (pokeInteractable != null)
            {
                // Usar solo el evento que existe en el SDK
                pokeInteractable.WhenPointerEventRaised += HandlePointerEvent;
            }
        }

        private void OnDisable()
        {
            if (pokeInteractable != null)
            {
                pokeInteractable.WhenPointerEventRaised -= HandlePointerEvent;
            }
        }

        private void Update()
        {
            // Animar la posición del botón
            AnimateButtonPosition();
        }
        #endregion

        #region Interaction Handling
        private void SetupInteractable()
        {
            if (pokeInteractable == null) return;

            // Configurar el PokeInteractable
            // EnterHoverNormal es la distancia desde la superficie en la que comienza el hover
            pokeInteractable.EnterHoverNormal = 0.1f; // Distancia para entrar en hover (medida en la normal de la superficie)

            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - PokeInteractable configured", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - Pointer Event: {evt.Type}", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);

            switch (evt.Type)
            {
                case PointerEventType.Hover:
                    OnHoverEnter();
                    break;

                case PointerEventType.Unhover:
                    OnHoverExit();
                    break;

                case PointerEventType.Select:
                    OnPokeStart();
                    break;

                case PointerEventType.Unselect:
                    OnPokeEnd();
                    break;
            }
        }
        #endregion

        #region Button Actions
        private void OnHoverEnter()
        {
            if (isHovered) return;

            isHovered = true;
            currentState = ButtonState.Hover;
            UpdateButtonVisual(currentState);

            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - Hover Enter", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }

        private void OnHoverExit()
        {
            if (!isHovered) return;

            isHovered = false;

            if (!isPressed)
            {
                currentState = ButtonState.Normal;
                UpdateButtonVisual(currentState);
            }

            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - Hover Exit", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }

        private void OnPokeStart()
        {
            if (isPressed) return;

            isPressed = true;
            currentState = ButtonState.Select;
            UpdateButtonVisual(currentState);

            // Invocar eventos
            OnButtonPressed?.Invoke();
            OnPressed?.Invoke();

            // Feedback
            PlaySound(pressSound);
            TriggerHaptics();

            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - Button Pressed!", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }

        private void OnPokeEnd()
        {
            if (!isPressed) return;

            isPressed = false;
            currentState = isHovered ? ButtonState.Hover : ButtonState.Normal;
            UpdateButtonVisual(currentState);

            // Invocar eventos
            OnButtonReleased?.Invoke();
            OnReleased?.Invoke();

            // Feedback
            PlaySound(releaseSound);

            AdvancedDebugSystem.Log($"[FlashlightButton] {name} - Button Released!", LogCategory.Flashlight | LogCategory.Photon, LogLevel.Debug);
        }
        #endregion

        #region Visual Updates
        private void UpdateButtonVisual(ButtonState state)
        {
            if (buttonRenderer == null) return;

            Material targetMaterial = null;

            switch (state)
            {
                case ButtonState.Normal:
                    targetMaterial = normalMaterial;
                    break;
                case ButtonState.Hover:
                    targetMaterial = hoveredMaterial ?? normalMaterial;
                    break;
                case ButtonState.Select:
                    targetMaterial = pressedMaterial ?? normalMaterial;
                    break;
            }

            if (targetMaterial != null)
            {
                buttonRenderer.material = targetMaterial;
            }
        }

        private void AnimateButtonPosition()
        {
            if (buttonTransform == null) return;

            Vector3 targetPosition = isPressed ? pressedPosition : originalPosition;
            buttonTransform.localPosition = Vector3.Lerp(
                buttonTransform.localPosition,
                targetPosition,
                Time.deltaTime * pressSpeed
            );
        }
        #endregion

        #region Feedback
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void TriggerHaptics()
        {
            // Obtener el controlador que está haciendo poke
            if (pokeInteractable.Interactors.Count > 0)
            {
                foreach (var interactor in pokeInteractable.Interactors)
                {
                    // Intentar obtener el componente XRBaseController del interactor
                    var xrController = interactor.GetComponentInParent<XRBaseController>();
                    if (xrController != null)
                    {
                        // Enviar impulso haptico usando XR Interaction Toolkit
                        xrController.SendHapticImpulse(hapticAmplitude, hapticDuration);
                    }
                    else
                    {
                        // Si no hay XRBaseController, intentar con el sistema de Meta/Oculus directamente
                        TriggerHapticsOVR(interactor);
                    }
                }
            }
        }

        private void TriggerHapticsOVR(object interactor)
        {
            // Fallback para haptics usando OVRInput si está disponible
            // Este método se mantiene como respaldo para compatibilidad con Meta SDK

            // Buscar si tenemos acceso a OVRInput
            var interactorTransform = (interactor as Component)?.transform;
            if (interactorTransform == null) return;

            // Determinar qué controlador está siendo usado basado en el nombre o tag
            bool isLeftHand = interactorTransform.name.ToLower().Contains("left");

            // Usar reflexión para evitar dependencia directa de OVR si no está disponible
            try
            {
                var ovrInputType = System.Type.GetType("OVRInput");
                if (ovrInputType != null)
                {
                    var controllerEnum = ovrInputType.GetNestedType("Controller");
                    if (controllerEnum != null)
                    {
                        var controller = isLeftHand ?
                            System.Enum.Parse(controllerEnum, "LTouch") :
                            System.Enum.Parse(controllerEnum, "RTouch");

                        var setVibrationMethod = ovrInputType.GetMethod("SetControllerVibration",
                            new[] { typeof(float), typeof(float), controllerEnum });

                        if (setVibrationMethod != null)
                        {
                            // Aplicar vibración
                            setVibrationMethod.Invoke(null, new object[] { hapticAmplitude, hapticAmplitude, controller });

                            // Programar el fin de la vibración
                            CancelHapticsAfterDelay(controller, controllerEnum, ovrInputType, hapticDuration);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogWarning($"[FlashlightButton] Could not trigger OVR haptics: {e.Message}", LogCategory.Flashlight | LogCategory.Photon);
            }
        }

        private async void CancelHapticsAfterDelay(object controller, System.Type controllerEnum, System.Type ovrInputType, float delay)
        {
            await System.Threading.Tasks.Task.Delay((int)(delay * 1000));

            try
            {
                var setVibrationMethod = ovrInputType.GetMethod("SetControllerVibration",
                    new[] { typeof(float), typeof(float), controllerEnum });

                if (setVibrationMethod != null)
                {
                    setVibrationMethod.Invoke(null, new object[] { 0f, 0f, controller });
                }
            }
            catch
            {
                // Silently fail if we can't stop the haptics
            }
        }
        #endregion

        #region Validation
        private void ValidateComponents()
        {
            if (pokeInteractable == null)
            {
                AdvancedDebugSystem.LogError($"[FlashlightButton] {name} - PokeInteractable component required!", LogCategory.Flashlight | LogCategory.Photon);
            }

            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponent<Renderer>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = 5f;
                }
            }

            if (normalMaterial == null && buttonRenderer != null)
            {
                normalMaterial = buttonRenderer.sharedMaterial;
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (buttonTransform == null) return;

            // Mostrar posición normal
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(buttonTransform.TransformPoint(originalPosition), 0.01f);

            // Mostrar posición presionada
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(buttonTransform.TransformPoint(pressedPosition), 0.01f);

            // Línea entre ambas posiciones
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                buttonTransform.TransformPoint(originalPosition),
                buttonTransform.TransformPoint(pressedPosition)
            );
        }
        #endregion
    }
}

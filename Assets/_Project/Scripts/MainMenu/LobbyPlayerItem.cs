using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Fusion;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Componente para el prefab que representa a un jugador en la lista del lobby
    /// </summary>
    public class LobbyPlayerItem : MonoBehaviour
    {
        [Header("UI Elements")] [SerializeField]
        private InteractableButton3D selectButton;

        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI playerStatusText;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Image backgroundPanel;

        [Header("Status Icons")] [SerializeField]
        private GameObject hostIcon;

        [SerializeField] private GameObject localPlayerIcon;
        [SerializeField] private GameObject pingIndicator;
        [SerializeField] private TextMeshProUGUI pingText;

        [Header("Visual Settings")] [SerializeField]
        private Color readyColor = Color.green;

        [SerializeField] private Color notReadyColor = Color.red;
        [SerializeField] private Color hostColor = new Color(1f, 0.8f, 0f); // Gold
        [SerializeField] private Color localPlayerColor = Color.cyan;
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

        [Header("Animation")] [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float animationDuration = 0.2f;

        // Private state
        private LobbyPlayer _playerData;
        private System.Action<LobbyPlayer> _onPlayerClicked;
        private bool _isSelected = false;
        private bool _isHovered = false;

        // Animation tweeners
        private Tween _readyPulseTween;
        private Tween _hoverTween;

        private void Awake()
        {
            // Configurar botón si existe
            if (selectButton != null)
            {
                selectButton.OnButtonPressed.AddListener(OnPlayerSelected);
                selectButton.OnButtonHovered.AddListener(OnPlayerHovered);
                selectButton.OnButtonUnhovered.AddListener(OnPlayerUnhovered);
            }
        }

        /// <summary>
        /// Inicializa el componente con callback para selección
        /// </summary>
        public void Initialize(System.Action<LobbyPlayer> onPlayerClicked)
        {
            _onPlayerClicked = onPlayerClicked;
        }

        /// <summary>
        /// Actualiza los datos mostrados del jugador
        /// </summary>
        public void UpdatePlayerData(LobbyPlayer playerData)
        {
            _playerData = playerData;

            if (playerData == null)
            {
                Debug.LogWarning("[LobbyPlayerItem] UpdatePlayerData called with null player");
                gameObject.SetActive(false);
                return;
            }
    
            Debug.Log($"[LobbyPlayerItem] Updating display for: {playerData.GetDisplayName()}");
    
            // Asegurar que el gameObject esté activo
            gameObject.SetActive(true);
    
            // Actualizar nombre
            UpdatePlayerName();

            // Actualizar estado (Ready/Not Ready)
            UpdatePlayerStatus();

            // Actualizar indicadores especiales
            UpdatePlayerIndicators();

            // Actualizar avatar
            UpdatePlayerAvatar();

            // Actualizar ping (placeholder)
            UpdatePingDisplay();

            // Animar cambios
            AnimateUpdate();
        }

        private void UpdatePlayerName()
        {
            if (playerNameText != null && _playerData != null)
            {
                string displayName = _playerData.GetDisplayName();
        
                // CORRECCIÓN: Verificar que el nombre no esté vacío
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = $"Player {_playerData.PlayerRef.PlayerId}";
                }
        
                playerNameText.text = displayName;

                // Color especial para jugador local
                if (_playerData.IsLocalPlayer)
                {
                    playerNameText.color = localPlayerColor;
                    Debug.Log($"[LobbyPlayerItem] Setting local player color for: {displayName}");
                }
                else if (_playerData.IsHost)
                {
                    playerNameText.color = hostColor;
                    Debug.Log($"[LobbyPlayerItem] Setting host color for: {displayName}");
                }
                else
                {
                    playerNameText.color = Color.white;
                }
            }
            else
            {
                Debug.LogWarning("[LobbyPlayerItem] playerNameText or _playerData is null in UpdatePlayerName");
            }
        }

        private void UpdatePlayerStatus()
        {
            if (_playerData == null) return;

            bool isReady = _playerData.IsReady;

            // Actualizar texto de estado
            if (playerStatusText != null)
            {
                playerStatusText.text = isReady ? "Ready" : "Not Ready";
                playerStatusText.color = isReady ? readyColor : notReadyColor;
            }

            // Actualizar indicador visual
            if (readyIndicator != null)
            {
                readyIndicator.color = isReady ? readyColor : notReadyColor;

                // Efecto de pulso si está ready
                if (isReady)
                {
                    StartReadyPulse();
                }
                else
                {
                    StopReadyPulse();
                }
            }
        }

        private void UpdatePlayerIndicators()
        {
            if (_playerData == null) return;

            // Mostrar/ocultar icono de host
            if (hostIcon != null)
            {
                hostIcon.SetActive(_playerData.IsHost);
            }

            // Mostrar/ocultar icono de jugador local
            if (localPlayerIcon != null)
            {
                localPlayerIcon.SetActive(_playerData.IsLocalPlayer);
            }

            // Actualizar fondo según tipo de jugador
            UpdateBackgroundColor();
        }

        private void UpdateBackgroundColor()
        {
            if (backgroundPanel == null || _playerData == null) return;

            Color targetColor = Color.clear;

            if (_isSelected)
            {
                targetColor = selectedColor;
            }
            else if (_playerData.IsLocalPlayer)
            {
                targetColor = new Color(localPlayerColor.r, localPlayerColor.g, localPlayerColor.b, 0.1f);
            }
            else if (_playerData.IsHost)
            {
                targetColor = new Color(hostColor.r, hostColor.g, hostColor.b, 0.1f);
            }

            backgroundPanel.DOColor(targetColor, animationDuration);
        }

        private void UpdatePlayerAvatar()
        {
            if (playerAvatar == null || _playerData == null) return;

            // Usar el color del jugador como avatar simple
            Color playerColor = _playerData.PlayerColor;
            playerAvatar.color = playerColor;

            // TODO: Aquí se podría integrar con Meta Avatars
            // En el futuro: playerAvatar.sprite = GetMetaAvatarSprite(_playerData.AvatarId);
        }

        private void UpdatePingDisplay()
        {
            // Placeholder para ping - en implementación real obtendríamos de Fusion
            if (pingIndicator != null)
            {
                pingIndicator.SetActive(true);

                // Simular ping para demo
                int fakePing = _playerData.IsLocalPlayer ? 0 : UnityEngine.Random.Range(30, 150);

                if (pingText != null)
                {
                    pingText.text = _playerData.IsLocalPlayer ? "LOCAL" : $"{fakePing}ms";

                    // Color según latencia
                    if (fakePing < 50)
                        pingText.color = readyColor;
                    else if (fakePing < 100)
                        pingText.color = Color.yellow;
                    else
                        pingText.color = notReadyColor;
                }
            }
        }

        #region Animation & Visual Effects

        private void AnimateUpdate()
        {
            // Pequeña animación cuando se actualiza
            transform.DOPunchScale(Vector3.one * 0.05f, 0.3f, 5, 0.3f);
        }

        private void StartReadyPulse()
        {
            if (readyIndicator == null) return;

            StopReadyPulse();
            _readyPulseTween = readyIndicator.transform.DOScale(1.2f, 0.8f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StopReadyPulse()
        {
            if (_readyPulseTween != null)
            {
                _readyPulseTween.Kill();
                _readyPulseTween = null;
            }

            if (readyIndicator != null)
            {
                readyIndicator.transform.localScale = Vector3.one;
            }
        }

        private void OnPlayerHovered()
        {
            if (_isHovered) return;

            _isHovered = true;

            // Efecto de hover
            if (_hoverTween != null) _hoverTween.Kill();
            _hoverTween = transform.DOScale(Vector3.one * hoverScale, animationDuration)
                .SetEase(Ease.OutQuad);

            // Cambiar opacidad del fondo
            if (backgroundPanel != null)
            {
                Color currentColor = backgroundPanel.color;
                currentColor.a = Mathf.Max(currentColor.a, 0.2f);
                backgroundPanel.DOColor(currentColor, animationDuration);
            }
        }

        private void OnPlayerUnhovered()
        {
            if (!_isHovered) return;

            _isHovered = false;

            // Revertir hover
            if (_hoverTween != null) _hoverTween.Kill();
            _hoverTween = transform.DOScale(Vector3.one, animationDuration)
                .SetEase(Ease.OutQuad);

            // Restaurar color de fondo
            UpdateBackgroundColor();
        }

        private void OnPlayerSelected()
        {
            // Efecto de selección
            transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);

            // Notificar selección
            _onPlayerClicked?.Invoke(_playerData);

            // Feedback háptico ligero
            OVRInput.SetControllerVibration(1, 0.2f, OVRInput.Controller.Touch);
            DOVirtual.DelayedCall(0.05f, () => OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Establece si este item está seleccionado
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateBackgroundColor();

            if (selected)
            {
                // Efecto visual adicional para selección
                if (backgroundPanel != null)
                {
                    backgroundPanel.transform.DOPunchScale(Vector3.one * 0.05f, 0.2f, 3, 0.3f);
                }
            }
        }

        /// <summary>
        /// Obtiene la referencia del jugador representado
        /// </summary>
        public PlayerRef GetPlayerRef()
        {
            return _playerData?.PlayerRef ?? PlayerRef.None;
        }

        /// <summary>
        /// Obtiene los datos del jugador
        /// </summary>
        public LobbyPlayer GetPlayerData()
        {
            return _playerData;
        }

        /// <summary>
        /// Verifica si representa al jugador local
        /// </summary>
        public bool IsLocalPlayer()
        {
            return _playerData?.IsLocalPlayer ?? false;
        }

        /// <summary>
        /// Verifica si representa al host
        /// </summary>
        public bool IsHost()
        {
            return _playerData?.IsHost ?? false;
        }

        /// <summary>
        /// Obtiene el estado ready del jugador
        /// </summary>
        public bool IsReady()
        {
            return _playerData?.IsReady ?? false;
        }

        #endregion

        #region Special Effects

        /// <summary>
        /// Reproduce efecto cuando el jugador cambia a ready
        /// </summary>
        public void PlayReadyEffect()
        {
            // Efecto de partículas verdes
            GameObject effect = new GameObject("ReadyEffect");
            effect.transform.SetParent(transform);
            effect.transform.localPosition = Vector3.zero;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startColor = readyColor;
            main.startSize = 0.02f;
            main.startLifetime = 1f;
            main.maxParticles = 20;

            var emission = particles.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0, 20)
            });

            particles.Play();

            // Destruir después de la animación
            Destroy(effect, 2f);

            // Efecto de escala
            transform.DOPunchScale(Vector3.one * 0.15f, 0.5f, 8, 0.3f);
        }

        /// <summary>
        /// Efecto visual cuando el jugador se une
        /// </summary>
        public void PlayJoinEffect()
        {
            // Animación de entrada
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.5f)
                .SetEase(Ease.OutBack)
                .SetDelay(UnityEngine.Random.Range(0f, 0.2f)); // Pequeño delay aleatorio

            // Efecto de brillo
            if (backgroundPanel != null)
            {
                Color originalColor = backgroundPanel.color;
                backgroundPanel.color = Color.white;
                backgroundPanel.DOColor(originalColor, 0.8f);
            }
        }

        /// <summary>
        /// Efecto visual cuando el jugador se va
        /// </summary>
        public void PlayLeaveEffect()
        {
            // Animación de salida
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));

            // Efecto de desvanecimiento
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0f, 0.3f);
        }

        #endregion

        #region Context Menu (Host Only)

        /// <summary>
        /// Muestra opciones disponibles para este jugador (kick, promote, etc.)
        /// Solo para hosts
        /// </summary>
        public void ShowPlayerOptions()
        {
            if (_playerData == null || _playerData.IsLocalPlayer) return;

            // TODO: Implementar menú contextual
            Debug.Log($"[LobbyPlayerItem] Showing options for {_playerData.GetDisplayName()}");

            // Opciones posibles:
            // - Kick Player
            // - Transfer Host
            // - Mute/Unmute
            // - View Profile
        }

        #endregion

        #region Debug Features

        [ContextMenu("Simulate Ready Toggle")]
        private void DebugToggleReady()
        {
            if (_playerData != null)
            {
                // Simular cambio de estado para testing
                bool newReadyState = !_playerData.IsReady;

                if (playerStatusText != null)
                {
                    playerStatusText.text = newReadyState ? "Ready" : "Not Ready";
                    playerStatusText.color = newReadyState ? readyColor : notReadyColor;
                }

                if (readyIndicator != null)
                {
                    readyIndicator.color = newReadyState ? readyColor : notReadyColor;

                    if (newReadyState)
                    {
                        StartReadyPulse();
                        PlayReadyEffect();
                    }
                    else
                    {
                        StopReadyPulse();
                    }
                }
            }
        }

        [ContextMenu("Test Join Effect")]
        private void DebugJoinEffect()
        {
            PlayJoinEffect();
        }

        [ContextMenu("Test Leave Effect")]
        private void DebugLeaveEffect()
        {
            PlayLeaveEffect();
        }
        
        [ContextMenu("Debug Components")]
        public void VerifyComponents()
        {
            Debug.Log("[LobbyPlayerItem] === Component Verification ===");
            Debug.Log($"  - playerNameText: {playerNameText != null}");
            Debug.Log($"  - playerStatusText: {playerStatusText != null}");
            Debug.Log($"  - playerAvatar: {playerAvatar != null}");
            Debug.Log($"  - readyIndicator: {readyIndicator != null}");
            Debug.Log($"  - backgroundPanel: {backgroundPanel != null}");
            Debug.Log($"  - selectButton: {selectButton != null}");
    
            if (playerNameText == null)
                Debug.LogError("[LobbyPlayerItem] ❌ playerNameText is not assigned!");
            if (playerStatusText == null)
                Debug.LogError("[LobbyPlayerItem] ❌ playerStatusText is not assigned!");
        }

        #endregion

        private void OnDestroy()
        {
            // Cleanup tweens
            _readyPulseTween?.Kill();
            _hoverTween?.Kill();

            // Remove button listeners
            if (selectButton != null)
            {
                selectButton.OnButtonPressed.RemoveAllListeners();
                selectButton.OnButtonHovered.RemoveAllListeners();
                selectButton.OnButtonUnhovered.RemoveAllListeners();
            }
        }

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Visualizar bounds del item en editor
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.3f, 0.1f));
        }

        #endregion
    }
}
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
    /// Usa paleta de colores simplificada: Amarillo (fondo), Blanco (texto principal), Gris (estados/secundario)
    /// </summary>
    public class LobbyPlayerItem : MonoBehaviour
    {
        [Header("UI Elements")] 
        [SerializeField] private InteractableButton3D selectButton;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI playerStatusText;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private Image readyIndicator;
        [SerializeField] private Image backgroundPanel;

        [Header("Status Icons")] 
        [SerializeField] private GameObject hostIcon;
        [SerializeField] private GameObject localPlayerIcon;
        [SerializeField] private GameObject pingIndicator;
        [SerializeField] private TextMeshProUGUI pingText;

        [Header("Color Palette")]
        [SerializeField] private Color primaryYellow = new Color(1f, 0.92f, 0.016f); // #FFD700 - Amarillo dorado
        [SerializeField] private Color textWhite = Color.white; // #FFFFFF - Blanco puro
        [SerializeField] private Color secondaryGray = new Color(0.5f, 0.5f, 0.5f); // #808080 - Gris medio
        
        [Header("Background Variations")]
        [SerializeField] private float yellowAlpha = 0.9f; // Opacidad del fondo amarillo
        [SerializeField] private float selectedAlphaMultiplier = 0.7f; // Reducir opacidad cuando está seleccionado
        [SerializeField] private float hoverAlphaMultiplier = 0.85f; // Reducir ligeramente al hover

        [Header("Animation")] 
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float animationDuration = 0.2f;
        
        private Vector3 _originalScale = Vector3.one;
        private bool _hasInitialized = false;

        private LobbyPlayer _playerData;
        private System.Action<LobbyPlayer> _onPlayerClicked;
        private bool _isSelected = false;
        private bool _isHovered = false;

        private Tween _readyPulseTween;
        private Tween _hoverTween;

        private void Awake()
        {
            if (!_hasInitialized)
            {
                _originalScale = transform.localScale;
                _hasInitialized = true;
            }
            
            if (selectButton != null)
            {
                selectButton.OnButtonPressed.AddListener(OnPlayerSelected);
                selectButton.OnButtonHovered.AddListener(OnPlayerHovered);
                selectButton.OnButtonUnhovered.AddListener(OnPlayerUnhovered);
            }
            
            // Establecer color base del fondo
            if (backgroundPanel != null)
            {
                Color bgColor = primaryYellow;
                bgColor.a = yellowAlpha;
                backgroundPanel.color = bgColor;
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
    
            gameObject.SetActive(true);
    
            UpdatePlayerName();
            UpdatePlayerStatus();
            UpdatePlayerIndicators();
            UpdatePlayerAvatar();
            UpdatePingDisplay();
            AnimateUpdate();
        }

        private void UpdatePlayerName()
        {
            if (playerNameText != null && _playerData != null)
            {
                string displayName = _playerData.GetDisplayName();
        
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = $"Player {_playerData.PlayerRef.PlayerId}";
                }
        
                playerNameText.text = displayName;
                
                // Todos los nombres en blanco para mantener la paleta limpia
                playerNameText.color = textWhite;
                
                // Agregar indicadores textuales para diferenciar roles
                if (_playerData.IsHost)
                {
                    displayName = $"★ {displayName}"; // Estrella para el host
                    playerNameText.text = displayName;
                }
                else if (_playerData.IsLocalPlayer)
                {
                    displayName = $"▶ {displayName}"; // Triángulo para jugador local
                    playerNameText.text = displayName;
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

            if (playerStatusText != null)
            {
                playerStatusText.text = isReady ? "Ready" : "Not Ready";
                // Ready en blanco, Not Ready en gris
                playerStatusText.color = isReady ? textWhite : secondaryGray;
            }

            if (readyIndicator != null)
            {
                // Indicador ready usa los mismos colores
                readyIndicator.color = isReady ? textWhite : secondaryGray;

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

            if (hostIcon != null)
            {
                hostIcon.SetActive(_playerData.IsHost);
                // Asegurar que el icono use color blanco
                var iconImage = hostIcon.GetComponent<Image>();
                if (iconImage != null) iconImage.color = textWhite;
            }

            if (localPlayerIcon != null)
            {
                localPlayerIcon.SetActive(_playerData.IsLocalPlayer);
                // Asegurar que el icono use color blanco
                var iconImage = localPlayerIcon.GetComponent<Image>();
                if (iconImage != null) iconImage.color = textWhite;
            }

            UpdateBackgroundColor();
        }

        private void UpdateBackgroundColor()
        {
            if (backgroundPanel == null) return;

            // Siempre usar amarillo como base
            Color targetColor = primaryYellow;
            
            // Ajustar alpha según estado
            if (_isSelected)
            {
                targetColor.a = yellowAlpha * selectedAlphaMultiplier;
            }
            else if (_isHovered)
            {
                targetColor.a = yellowAlpha * hoverAlphaMultiplier;
            }
            else
            {
                targetColor.a = yellowAlpha;
            }

            backgroundPanel.DOColor(targetColor, animationDuration);
        }

        private void UpdatePlayerAvatar()
        {
            if (playerAvatar == null || _playerData == null) return;

            // Avatar en blanco o gris según el estado
            if (_playerData.IsReady)
            {
                playerAvatar.color = textWhite;
            }
            else
            {
                playerAvatar.color = secondaryGray;
            }
        }

        private void UpdatePingDisplay()
        {
            if (pingIndicator != null)
            {
                pingIndicator.SetActive(true);

                int fakePing = _playerData.IsLocalPlayer ? 0 : UnityEngine.Random.Range(30, 150);

                if (pingText != null)
                {
                    pingText.text = _playerData.IsLocalPlayer ? "LOCAL" : $"{fakePing}ms";

                    // Usar solo blanco y gris para el ping
                    if (_playerData.IsLocalPlayer || fakePing < 100)
                    {
                        pingText.color = textWhite;
                    }
                    else
                    {
                        pingText.color = secondaryGray;
                    }
                }
            }
        }

        #region Animation & Visual Effects

        private void AnimateUpdate()
        {
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

            if (_hoverTween != null) _hoverTween.Kill();
            _hoverTween = transform.DOScale(Vector3.one * hoverScale, animationDuration)
                .SetEase(Ease.OutQuad);

            UpdateBackgroundColor();
        }

        private void OnPlayerUnhovered()
        {
            if (!_isHovered) return;

            _isHovered = false;

            if (_hoverTween != null) _hoverTween.Kill();
            _hoverTween = transform.DOScale(Vector3.one, animationDuration)
                .SetEase(Ease.OutQuad);

            UpdateBackgroundColor();
        }

        private void OnPlayerSelected()
        {
            transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);

            _onPlayerClicked?.Invoke(_playerData);

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
            GameObject effect = new GameObject("ReadyEffect");
            effect.transform.SetParent(transform);
            effect.transform.localPosition = Vector3.zero;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startColor = textWhite; // Partículas blancas
            main.startSize = 0.02f;
            main.startLifetime = 1f;
            main.maxParticles = 20;

            var emission = particles.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0, 20)
            });

            particles.Play();

            Destroy(effect, 2f);

            transform.DOPunchScale(Vector3.one * 0.15f, 0.5f, 8, 0.3f);
        }

        /// <summary>
        /// Efecto visual cuando el jugador se une
        /// </summary>
        public void PlayJoinEffect()
        {
            // SIEMPRE resetear a la escala original primero
            transform.localScale = Vector3.zero;
            transform.DOScale(_originalScale, 0.5f) // Usar escala original
                .SetEase(Ease.OutBack)
                .SetDelay(UnityEngine.Random.Range(0f, 0.2f));

            if (backgroundPanel != null)
            {
                Color originalColor = primaryYellow;
                originalColor.a = yellowAlpha;
                backgroundPanel.color = textWhite;
                backgroundPanel.DOColor(originalColor, 0.8f);
            }
        }
        
        public void ResetItem()
        {
            transform.DOKill();
            transform.localScale = _originalScale;
            _isSelected = false;
            _isHovered = false;
        
            // Resetear estados visuales
            if (readyIndicator != null)
            {
                readyIndicator.transform.DOKill();
                readyIndicator.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Efecto visual cuando el jugador se va
        /// </summary>
        public void PlayLeaveEffect()
        {
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false));

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
        }

        #endregion

        #region Debug Features

        [ContextMenu("Simulate Ready Toggle")]
        private void DebugToggleReady()
        {
            if (_playerData != null)
            {
                bool newReadyState = !_playerData.IsReady;

                if (playerStatusText != null)
                {
                    playerStatusText.text = newReadyState ? "Ready" : "Not Ready";
                    playerStatusText.color = newReadyState ? textWhite : secondaryGray;
                }

                if (readyIndicator != null)
                {
                    readyIndicator.color = newReadyState ? textWhite : secondaryGray;

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

        [ContextMenu("Apply Color Palette")]
        private void ApplyColorPalette()
        {
            // Aplicar paleta de colores en el editor
            if (backgroundPanel != null)
            {
                Color bgColor = primaryYellow;
                bgColor.a = yellowAlpha;
                backgroundPanel.color = bgColor;
            }
            
            if (playerNameText != null)
                playerNameText.color = textWhite;
                
            if (playerStatusText != null)
                playerStatusText.color = secondaryGray;
                
            if (playerAvatar != null)
                playerAvatar.color = secondaryGray;
                
            if (readyIndicator != null)
                readyIndicator.color = secondaryGray;
                
            if (pingText != null)
                pingText.color = textWhite;
        }

        #endregion

        private void OnDestroy()
        {
            _readyPulseTween?.Kill();
            _hoverTween?.Kill();

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
            // Gizmo en amarillo para mantener consistencia
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f);
            Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.3f, 0.1f));
        }

        #endregion
    }
}
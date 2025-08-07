using DG.Tweening;
using Fusion;
using HackMonkeys.UI.Spatial;
using HackMonkeys.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Componente para items individuales de sala en la lista
    /// Versión refactorizada con actualización incremental y preservación de estado
    /// </summary>
    public class RoomItem : MonoBehaviour
    {
        [Header("UI Elements")] 
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private Image statusIndicator;
        [SerializeField] private InteractableButton3D selectButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject playingIcon;
        
        [Header("Visual Feedback")]
        [SerializeField] private GameObject updateIndicator;
        [SerializeField] private ParticleSystem updateParticles;

        [Header("Animation")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float animationDuration = 0.2f;
        [SerializeField] private float updatePulseDuration = 0.3f;

        // Estado interno
        private SessionInfo _sessionInfo;
        private SessionInfo _previousSessionInfo;
        private System.Action<SessionInfo> _onSelected;
        private UIColorTheme _colorTheme;
        private int _index;
        
        // Tracking de estado
        private bool _isSelected = false;
        private bool _isHovered = false;
        private bool _isInitialized = false;
        
        // Cache para evitar actualizaciones innecesarias
        private string _lastKnownName;
        private int _lastPlayerCount = -1;
        private bool _lastOpenState = true;
        private bool _lastFullState = false;
        
        // Animaciones activas
        private Tweener _hoverTween;
        private Tweener _updateTween;
        private Sequence _updateSequence;

        /// <summary>
        /// Inicializa el componente con callbacks y tema
        /// </summary>
        public void Initialize(int index, System.Action<SessionInfo> onSelected, UIColorTheme colorTheme)
        {
            _index = index;
            _onSelected = onSelected;
            _colorTheme = colorTheme ?? UIColorTheme.Instance;

            if (selectButton != null)
            {
                selectButton.OnButtonPressed.RemoveAllListeners();
                selectButton.OnButtonPressed.AddListener(OnSelectPressed);
                selectButton.OnButtonHovered.AddListener(OnHoverEnter);
                selectButton.OnButtonUnhovered.AddListener(OnHoverExit);
            }
            
            // Aplicar color de fondo base
            if (backgroundImage != null)
            {
                backgroundImage.color = _colorTheme.GetBackgroundColor();
            }
            
            // Aplicar colores a textos
            ApplyTextColors();
            
            // Ocultar indicadores inicialmente
            if (updateIndicator != null)
                updateIndicator.SetActive(false);
                
            if (lockIcon != null)
                lockIcon.SetActive(false);
                
            if (playingIcon != null)
                playingIcon.SetActive(false);
                
            _isInitialized = true;
        }

        private void ApplyTextColors()
        {
            if (roomNameText != null)
                roomNameText.color = _colorTheme.TextWhite;
                
            if (playerCountText != null)
                playerCountText.color = _colorTheme.TextWhite;
                
            if (pingText != null)
                pingText.color = _colorTheme.TextWhite;
        }

        /// <summary>
        /// Establece los datos de la sala (configuración completa)
        /// </summary>
        public void SetRoomData(SessionInfo session)
        {
            if (session == null)
            {
                Debug.LogWarning("[RoomItem] SetRoomData called with null session");
                return;
            }
            
            // Guardar estado anterior para comparación
            _previousSessionInfo = _sessionInfo;
            _sessionInfo = session;
            
            // Actualizar todos los elementos visuales
            UpdateRoomName(session.Name);
            UpdatePlayerCount(session.PlayerCount, session.MaxPlayers);
            UpdatePingDisplay();
            UpdateRoomState(session.IsOpen, session.PlayerCount >= session.MaxPlayers);
            
            // Cache de valores
            _lastKnownName = session.Name;
            _lastPlayerCount = session.PlayerCount;
            _lastOpenState = session.IsOpen;
            _lastFullState = session.PlayerCount >= session.MaxPlayers;
            
            // Resetear selección
            SetSelected(false);
            
            // Animación de aparición si es la primera vez
            if (_previousSessionInfo == null && _isInitialized)
            {
                PlayAppearAnimation();
            }
        }

        /// <summary>
        /// Actualiza los datos de la sala (actualización incremental)
        /// </summary>
        public void UpdateRoomData(SessionInfo session)
        {
            if (session == null) return;
            
            // Si es la misma sala, actualización incremental
            if (_sessionInfo != null && _sessionInfo.Name == session.Name)
            {
                UpdateIncrementalData(session);
            }
            else
            {
                // Es una sala diferente, actualización completa
                SetRoomData(session);
            }
        }

        /// <summary>
        /// Actualización incremental - solo actualiza lo que cambió
        /// </summary>
        private void UpdateIncrementalData(SessionInfo session)
        {
            bool hasChanges = false;
            
            // Verificar cambios en nombre (poco probable pero posible)
            if (session.Name != _lastKnownName)
            {
                UpdateRoomName(session.Name);
                _lastKnownName = session.Name;
                hasChanges = true;
            }
            
            // Verificar cambios en contador de jugadores
            if (session.PlayerCount != _lastPlayerCount)
            {
                UpdatePlayerCount(session.PlayerCount, session.MaxPlayers);
                _lastPlayerCount = session.PlayerCount;
                hasChanges = true;
                
                // Efecto especial si la sala se llenó o se liberó espacio
                bool wasFullBefore = _lastFullState;
                bool isFullNow = session.PlayerCount >= session.MaxPlayers;
                
                if (!wasFullBefore && isFullNow)
                {
                    PlayRoomFullEffect();
                }
                else if (wasFullBefore && !isFullNow)
                {
                    PlaySpaceAvailableEffect();
                }
                
                _lastFullState = isFullNow;
            }
            
            // Verificar cambios en estado abierto/cerrado
            if (session.IsOpen != _lastOpenState)
            {
                UpdateRoomState(session.IsOpen, session.PlayerCount >= session.MaxPlayers);
                _lastOpenState = session.IsOpen;
                hasChanges = true;
            }
            
            // Actualizar referencia
            _sessionInfo = session;
            
            // Efecto visual si hubo cambios
            if (hasChanges)
            {
                PlayUpdateAnimation();
            }
        }

        /// <summary>
        /// Actualización en tiempo real (para cambios de red)
        /// </summary>
        public void UpdateRealtimeData(int playerCount, bool isOpen)
        {
            if (_sessionInfo == null) return;
            
            bool hasChanges = false;
            
            // Actualizar contador si cambió
            if (playerCount != _lastPlayerCount)
            {
                UpdatePlayerCount(playerCount, _sessionInfo.MaxPlayers);
                _lastPlayerCount = playerCount;
                hasChanges = true;
                
                // Verificar si se llenó o liberó
                bool wasFullBefore = _lastFullState;
                bool isFullNow = playerCount >= _sessionInfo.MaxPlayers;
                
                if (!wasFullBefore && isFullNow)
                {
                    PlayRoomFullEffect();
                }
                else if (wasFullBefore && !isFullNow)
                {
                    PlaySpaceAvailableEffect();
                }
                
                _lastFullState = isFullNow;
            }
            
            // Actualizar estado si cambió
            if (isOpen != _lastOpenState)
            {
                UpdateRoomState(isOpen, playerCount >= _sessionInfo.MaxPlayers);
                _lastOpenState = isOpen;
                hasChanges = true;
            }
            
            // Mostrar indicador de actualización en tiempo real
            if (hasChanges)
            {
                ShowRealtimeUpdateIndicator();
            }
        }

        #region Visual Updates

        private void UpdateRoomName(string name)
        {
            if (roomNameText != null)
            {
                // Usar fallback si el nombre está vacío
                string displayName = !string.IsNullOrEmpty(name) ? name : "Unnamed Room";
                roomNameText.text = displayName;
        
                Debug.Log($"[RoomItem] Setting room name: {displayName}");
            }
        }

        private void UpdatePlayerCount(int current, int max)
        {
            if (playerCountText == null) return;
            
            playerCountText.text = $"{current}/{max}";
            playerCountText.color = _colorTheme.GetPlayerCountColor(current, max);
            
            // Animación sutil cuando cambia
            if (_lastPlayerCount != -1 && current != _lastPlayerCount)
            {
                playerCountText.transform.DOKill();
                playerCountText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
            }
        }

        private void UpdatePingDisplay()
        {
            if (pingText != null)
            {
                // TODO: Usar ping real cuando esté disponible
                int simulatedPing = UnityEngine.Random.Range(20, 120);
                pingText.text = $"{simulatedPing}ms";
                pingText.color = _colorTheme.GetPingColor(simulatedPing);
            }
        }

        private void UpdateRoomState(bool isOpen, bool isFull)
        {
            if (statusIndicator != null)
            {
                statusIndicator.color = _colorTheme.GetRoomStateColor(isOpen, isFull);
                
                // Animación de cambio de estado
                if (_lastOpenState != isOpen)
                {
                    statusIndicator.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f);
                }
            }
            
            // Actualizar iconos
            if (lockIcon != null)
                lockIcon.SetActive(!isOpen && isFull);
                
            if (playingIcon != null)
                playingIcon.SetActive(!isOpen && !isFull);
        }

        #endregion

        #region Animations and Effects

        private void PlayAppearAnimation()
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, animationDuration)
                .SetDelay(_index * 0.05f)
                .SetEase(Ease.OutBack);
        }

        private void PlayUpdateAnimation()
        {
            // Cancelar animación anterior si existe
            _updateTween?.Kill();
            
            // Pulso sutil para indicar actualización
            _updateTween = transform.DOPunchScale(Vector3.one * 0.02f, updatePulseDuration, 2, 0.5f);
            
            // Mostrar indicador temporal
            if (updateIndicator != null)
            {
                updateIndicator.SetActive(true);
                updateIndicator.transform.localScale = Vector3.zero;
                
                _updateSequence?.Kill();
                _updateSequence = DOTween.Sequence();
                _updateSequence.Append(updateIndicator.transform.DOScale(Vector3.one, 0.2f));
                _updateSequence.AppendInterval(0.5f);
                _updateSequence.Append(updateIndicator.transform.DOScale(Vector3.zero, 0.2f));
                _updateSequence.OnComplete(() => updateIndicator.SetActive(false));
            }
        }

        private void ShowRealtimeUpdateIndicator()
        {
            // Partículas para actualización en tiempo real
            if (updateParticles != null)
            {
                updateParticles.Emit(5);
            }
            
            // Flash sutil del background
            if (backgroundImage != null)
            {
                Color originalColor = backgroundImage.color;
                backgroundImage.DOColor(_colorTheme.TextWhite, 0.1f)
                    .OnComplete(() => backgroundImage.DOColor(originalColor, 0.2f));
            }
        }

        private void PlayRoomFullEffect()
        {
            // Efecto cuando la sala se llena
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(_colorTheme.SecondaryGray, animationDuration);
            }
            
            // Vibración haptica si está seleccionada
            if (_isSelected)
            {
                OVRInput.SetControllerVibration(1, 0.2f, OVRInput.Controller.Touch);
                DOVirtual.DelayedCall(0.1f, () => 
                    OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
            }
        }

        private void PlaySpaceAvailableEffect()
        {
            // Efecto cuando se libera espacio
            if (backgroundImage != null)
            {
                Color targetColor = _isSelected ? 
                    _colorTheme.GetSelectedColor() : 
                    _colorTheme.GetBackgroundColor();
                    
                backgroundImage.DOColor(targetColor, animationDuration);
            }
            
            // Partículas de celebración
            if (updateParticles != null)
            {
                updateParticles.Emit(10);
            }
        }

        #endregion

        #region Interaction Handlers

        private void OnSelectPressed()
        {
            if (_sessionInfo == null) return;
            
            _onSelected?.Invoke(_sessionInfo);
            SetSelected(true);
            
            // Haptic feedback
            OVRInput.SetControllerVibration(1, 0.1f, OVRInput.Controller.Touch);
            DOVirtual.DelayedCall(0.05f, () => 
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
        }

        private void OnHoverEnter()
        {
            if (_isHovered) return;
            
            _isHovered = true;
            
            if (backgroundImage != null && !_isSelected)
            {
                backgroundImage.DOColor(_colorTheme.GetHoverColor(), animationDuration);
            }
            
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(Vector3.one * hoverScale, animationDuration)
                .SetEase(Ease.OutQuad);
        }

        private void OnHoverExit()
        {
            if (!_isHovered) return;
            
            _isHovered = false;
            
            if (backgroundImage != null && !_isSelected)
            {
                backgroundImage.DOColor(_colorTheme.GetBackgroundColor(), animationDuration);
            }
            
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(Vector3.one, animationDuration)
                .SetEase(Ease.OutQuad);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Establece el estado de selección del item
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (backgroundImage != null)
            {
                Color targetColor = selected ? 
                    _colorTheme.GetSelectedColor() : 
                    (_isHovered ? _colorTheme.GetHoverColor() : _colorTheme.GetBackgroundColor());
                    
                backgroundImage.DOColor(targetColor, animationDuration);
            }
            
            if (selected)
            {
                transform.DOPunchScale(Vector3.one * 0.05f, 0.3f, 5, 0.5f);
            }
        }

        /// <summary>
        /// Obtiene la información de la sesión actual
        /// </summary>
        public SessionInfo GetSessionInfo()
        {
            return _sessionInfo;
        }

        /// <summary>
        /// Verifica si el item representa una sala específica
        /// </summary>
        public bool RepresentsRoom(string roomName)
        {
            return _sessionInfo != null && _sessionInfo.Name == roomName;
        }

        /// <summary>
        /// Limpia el item para reutilización
        /// </summary>
        public void Clear()
        {
            _sessionInfo = null;
            _previousSessionInfo = null;
            _lastKnownName = null;
            _lastPlayerCount = -1;
            _lastOpenState = true;
            _lastFullState = false;
            _isSelected = false;
            _isHovered = false;
            
            gameObject.SetActive(false);
        }

        #endregion

        #region Debug

        [ContextMenu("Simulate Player Join")]
        private void DebugSimulatePlayerJoin()
        {
            if (_sessionInfo != null && _sessionInfo.PlayerCount < _sessionInfo.MaxPlayers)
            {
                UpdateRealtimeData(_sessionInfo.PlayerCount + 1, _sessionInfo.IsOpen);
            }
        }
        
        [ContextMenu("Simulate Player Leave")]
        private void DebugSimulatePlayerLeave()
        {
            if (_sessionInfo != null && _sessionInfo.PlayerCount > 0)
            {
                UpdateRealtimeData(_sessionInfo.PlayerCount - 1, _sessionInfo.IsOpen);
            }
        }
        
        [ContextMenu("Simulate Room Close")]
        private void DebugSimulateRoomClose()
        {
            if (_sessionInfo != null)
            {
                UpdateRealtimeData(_sessionInfo.PlayerCount, false);
            }
        }
        
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"=== RoomItem State ===");
            Debug.Log($"Room: {_sessionInfo?.Name ?? "None"}");
            Debug.Log($"Players: {_lastPlayerCount}");
            Debug.Log($"Is Open: {_lastOpenState}");
            Debug.Log($"Is Full: {_lastFullState}");
            Debug.Log($"Is Selected: {_isSelected}");
            Debug.Log($"Is Hovered: {_isHovered}");
            Debug.Log($"======================");
        }

        #endregion

        private void OnDestroy()
        {
            // Limpiar tweens
            _hoverTween?.Kill();
            _updateTween?.Kill();
            _updateSequence?.Kill();
            
            transform.DOKill();
            if (backgroundImage != null)
                backgroundImage.DOKill();
            if (playerCountText != null)
                playerCountText.transform.DOKill();
            if (statusIndicator != null)
                statusIndicator.transform.DOKill();
            if (updateIndicator != null)
                updateIndicator.transform.DOKill();
            
            // Limpiar listeners
            if (selectButton != null)
            {
                selectButton.OnButtonPressed.RemoveAllListeners();
                selectButton.OnButtonHovered.RemoveAllListeners();
                selectButton.OnButtonUnhovered.RemoveAllListeners();
            }
        }
    }
}
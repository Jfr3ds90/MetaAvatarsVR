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
    /// Actualizado con paleta de colores unificada y sincronización de estados
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
        [SerializeField] private GameObject lockIcon; // Para salas cerradas
        [SerializeField] private GameObject playingIcon; // Para salas en juego

        [Header("Animation")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float animationDuration = 0.2f;

        private SessionInfo _sessionInfo;
        private System.Action<SessionInfo> _onSelected;
        private UIColorTheme _colorTheme;
        private int _index;
        private bool _isSelected = false;
        private bool _isHovered = false;
        
        // Cache de estados para evitar actualizaciones innecesarias
        private int _lastPlayerCount = -1;
        private bool _lastOpenState = true;

        public void Initialize(int index, System.Action<SessionInfo> onSelected, UIColorTheme colorTheme)
        {
            _index = index;
            _onSelected = onSelected;
            _colorTheme = colorTheme ?? UIColorTheme.Instance;

            if (selectButton != null)
            {
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

        public void SetRoomData(SessionInfo session)
        {
            _sessionInfo = session;

            // Actualizar nombre de sala
            if (roomNameText != null)
                roomNameText.text = session.Name;

            // Actualizar contador de jugadores con sincronización
            UpdatePlayerCount(session.PlayerCount, session.MaxPlayers);

            // Actualizar ping
            if (pingText != null)
            {
                // TODO: Usar ping real cuando esté disponible
                int simulatedPing = UnityEngine.Random.Range(20, 120);
                pingText.text = $"{simulatedPing}ms";
                pingText.color = _colorTheme.GetPingColor(simulatedPing);
            }

            // Actualizar estado de la sala
            UpdateRoomState(session.IsOpen, session.PlayerCount >= session.MaxPlayers);

            // Resetear selección
            SetSelected(false);
            
            // Cache de estados
            _lastPlayerCount = session.PlayerCount;
            _lastOpenState = session.IsOpen;
        }

        private void UpdatePlayerCount(int current, int max)
        {
            if (playerCountText == null) return;
            
            // Solo actualizar si cambió
            if (current == _lastPlayerCount) return;
            
            playerCountText.text = $"{current}/{max}";
            
            // Usar color basado en capacidad
            playerCountText.color = _colorTheme.GetPlayerCountColor(current, max);
            
            // Animación cuando cambia el contador
            if (_lastPlayerCount != -1 && current != _lastPlayerCount)
            {
                playerCountText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
                
                // Efecto visual si se llenó la sala
                if (current >= max && _lastPlayerCount < max)
                {
                    ShowRoomFullEffect();
                }
            }
        }

        private void UpdateRoomState(bool isOpen, bool isFull)
        {
            if (statusIndicator == null) return;
            
            // Color del indicador según estado
            statusIndicator.color = _colorTheme.GetRoomStateColor(isOpen, isFull);
            
            // Iconos de estado
            if (lockIcon != null)
                lockIcon.SetActive(!isOpen);
                
            if (playingIcon != null)
                playingIcon.SetActive(!isOpen && !isFull); // En juego
                
            // Si cambió el estado, animar
            if (isOpen != _lastOpenState)
            {
                statusIndicator.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f);
            }
        }

        private void OnSelectPressed()
        {
            _onSelected?.Invoke(_sessionInfo);
            SetSelected(true);
            
            // Haptic feedback
            OVRInput.SetControllerVibration(1, 0.1f, OVRInput.Controller.Touch);
            DOVirtual.DelayedCall(0.05f, () => OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));
        }

        private void OnHoverEnter()
        {
            if (_isHovered) return;
            
            _isHovered = true;
            
            if (backgroundImage != null)
            {
                backgroundImage.DOColor(_colorTheme.GetHoverColor(), animationDuration);
            }
            
            transform.DOScale(Vector3.one * hoverScale, animationDuration).SetEase(Ease.OutQuad);
        }

        private void OnHoverExit()
        {
            if (!_isHovered) return;
            
            _isHovered = false;
            
            if (backgroundImage != null && !_isSelected)
            {
                backgroundImage.DOColor(_colorTheme.GetBackgroundColor(), animationDuration);
            }
            
            transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutQuad);
        }

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

        private void ShowRoomFullEffect()
        {
            // Crear efecto visual cuando la sala se llena
            GameObject fullEffect = new GameObject("RoomFullEffect");
            fullEffect.transform.SetParent(transform);
            fullEffect.transform.localPosition = Vector3.zero;
            
            // Añadir componente de imagen para el flash
            Image flashImage = fullEffect.AddComponent<Image>();
            flashImage.color = new Color(1f, 1f, 1f, 0.3f);
            flashImage.raycastTarget = false;
            
            RectTransform rect = fullEffect.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            // Animar el flash
            flashImage.DOFade(0f, 0.5f).OnComplete(() => Destroy(fullEffect));
        }

        /// <summary>
        /// Actualizar datos en tiempo real (para sincronización multijugador)
        /// </summary>
        public void UpdateRealtimeData(int playerCount, bool isOpen)
        {
            if (_sessionInfo == null) return;
            
            // Actualizar datos internos
            //_sessionInfo.PlayerCount = playerCount;
            _sessionInfo.IsOpen = isOpen;
            
            // Actualizar UI solo si cambió
            if (playerCount != _lastPlayerCount)
            {
                UpdatePlayerCount(playerCount, _sessionInfo.MaxPlayers);
                _lastPlayerCount = playerCount;
            }
            
            if (isOpen != _lastOpenState)
            {
                UpdateRoomState(isOpen, playerCount >= _sessionInfo.MaxPlayers);
                _lastOpenState = isOpen;
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.OnButtonPressed.RemoveListener(OnSelectPressed);
                selectButton.OnButtonHovered.RemoveListener(OnHoverEnter);
                selectButton.OnButtonUnhovered.RemoveListener(OnHoverExit);
            }
            
            transform.DOKill();
            if (backgroundImage != null)
                backgroundImage.DOKill();
        }

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
        
        #endregion
    }
}
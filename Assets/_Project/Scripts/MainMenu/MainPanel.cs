using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using Meta.XR;

namespace HackMonkeys.UI.Panels
{
    public class MainPanel : MenuPanel
    {
        [Header("UI Elements")]
        [SerializeField] private InteractableButton3D playButton;
        [SerializeField] private InteractableButton3D hostButton;
        [SerializeField] private InteractableButton3D friendsButton;
        [SerializeField] private InteractableButton3D settingsButton;
        [SerializeField] private InteractableButton3D optionsButton;
        [SerializeField] private InteractableButton3D exitButton;
        
        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        
        [Header("Logo & Title")]
        [SerializeField] private Transform logoTransform;
        [SerializeField] private TextMeshProUGUI gameTitle;
        [SerializeField] private ParticleSystem logoParticles;
        
        private PlayerDataManager _dataManager;
        private NetworkBootstrapper _networkBootstrapper;
        
        // Referencias a las animaciones para poder detenerlas
        private Tweener _logoRotationTween;
        private Tweener _logoMovementTween;
        private Vector3 _logoOriginalPosition;
        
        protected override void SetupPanel()
        {
            base.SetupPanel();
            
            _dataManager = PlayerDataManager.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            // Guardar posición original del logo
            if (logoTransform != null)
            {
                _logoOriginalPosition = logoTransform.localPosition;
            }
            
            UpdatePlayerInfo();
        }

        protected override void ConfigureButtons()
        {
            base.ConfigureButtons();
            
            if (playButton != null)
            {
                playButton.OnButtonPressed.RemoveAllListeners();
                playButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.LobbyBrowser);
                });
            }
            
            if (hostButton != null)
            {
                hostButton.OnButtonPressed.RemoveAllListeners();
                hostButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.CreateLobby);
                });
            }
            
            if (friendsButton != null)
            {
                friendsButton.OnButtonPressed.RemoveAllListeners();
                friendsButton.OnButtonPressed.AddListener(() =>
                {
                    _uiManager.ShowPanel(PanelID.Friends);
                });
            }
            
            if (settingsButton != null)
            {
                settingsButton.OnButtonPressed.RemoveAllListeners();
                settingsButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.Settings);
                });
            }
            
            if (optionsButton != null)
            {
                optionsButton.OnButtonPressed.RemoveAllListeners();
                optionsButton.OnButtonPressed.AddListener(() =>
                {
                    _uiManager.ShowPanel(PanelID.Options);
                });
            }
            
            if (exitButton != null)
            {
                exitButton.OnButtonPressed.RemoveAllListeners();
                exitButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.ExitPanel);
                });
            }
        }
        
        private void UpdatePlayerInfo()
        {
            if (playerNameText != null && _dataManager != null)
            {
                string playerName = _dataManager.GetPlayerName();
                playerNameText.text = string.IsNullOrEmpty(playerName) ? "Guest Player" : playerName;
            }
            
            UpdateConnectionStatus();
            
            if (playerAvatar != null)
            {
                Color avatarColor = GetPlayerColor();
                playerAvatar.color = avatarColor;
            }
        }
        
        private void UpdateConnectionStatus()
        {
            if (connectionStatusText == null) return;
            
            if (_networkBootstrapper != null && _networkBootstrapper.IsConnected)
            {
                connectionStatusText.text = "<color=green>● Connected</color>";
                
                if (playButton != null) playButton.SetInteractable(true);
                if (hostButton != null) hostButton.SetInteractable(true);
            }
            else
            {
                connectionStatusText.text = "<color=red>● Connecting...</color>";
                
                if (playButton != null) playButton.SetInteractable(false);
                if (hostButton != null) hostButton.SetInteractable(false);
            }
        }
        
        private Color GetPlayerColor()
        {
            string playerName = _dataManager?.GetPlayerName() ?? "Guest";
            int hash = playerName.GetHashCode();
            Random.InitState(hash);
            
            return new Color(
                Random.Range(0.3f, 0.9f),
                Random.Range(0.3f, 0.9f),
                Random.Range(0.3f, 0.9f)
            );
        }
        
        private void AnimateLogo()
        {
            if (logoTransform == null) return;
            
            // Detener animaciones anteriores
            StopLogoAnimations();
            
            // Resetear posición
            logoTransform.localPosition = _logoOriginalPosition;
            
            // Crear nuevas animaciones y guardar referencias
            _logoRotationTween = logoTransform
                .DORotate(new Vector3(0, 360, 0), 20f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
            
            _logoMovementTween = logoTransform
                .DOLocalMoveY(_logoOriginalPosition.y + 0.1f, 2f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            
            if (logoParticles != null)
            {
                logoParticles.Play();
            }
        }
        
        private void StopLogoAnimations()
        {
            // Detener y limpiar animaciones
            _logoRotationTween?.Kill();
            _logoMovementTween?.Kill();
            
            _logoRotationTween = null;
            _logoMovementTween = null;
            
            // Detener partículas
            if (logoParticles != null)
            {
                logoParticles.Stop();
            }
        }
        
        public override void OnPanelShown()
        {
            base.OnPanelShown();
            
            UpdateConnectionStatus();
            
            // Iniciar animación del logo
            AnimateLogo();
            
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnConnectedToServerEvent.RemoveListener(OnConnected);
                _networkBootstrapper.OnConnectionFailed.RemoveListener(OnConnectionFailed);
                
                _networkBootstrapper.OnConnectedToServerEvent.AddListener(OnConnected);
                _networkBootstrapper.OnConnectionFailed.AddListener(OnConnectionFailed);
            }
            
            if (gameTitle != null)
            {
                gameTitle.transform.localScale = Vector3.zero;
                gameTitle.transform.DOScale(Vector3.one, 0.5f)
                    .SetDelay(0.2f)
                    .SetEase(Ease.OutBack);
            }
        }
        
        public override void OnPanelHidden()
        {
            base.OnPanelHidden();
            
            // IMPORTANTE: Detener animaciones cuando se oculta el panel
            StopLogoAnimations();
            
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnConnectedToServerEvent.RemoveListener(OnConnected);
                _networkBootstrapper.OnConnectionFailed.RemoveListener(OnConnectionFailed);
            }
        }
        
        private void OnConnected()
        {
            UpdateConnectionStatus();
            ShowNotification("Connected to server!", NotificationType.Success);
        }
        
        private void OnConnectionFailed(string error)
        {
            UpdateConnectionStatus();
            ShowNotification($"Connection failed: {error}", NotificationType.Error);
        }
          
        private void ShowNotification(string message, NotificationType type)
        {
            Debug.Log($"[Notification] {type}: {message}");
        }
        
        private enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }
        
        // Asegurar limpieza cuando se destruye
        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopLogoAnimations();
            
            // Limpiar todos los listeners
            playButton?.OnButtonPressed.RemoveAllListeners();
            hostButton?.OnButtonPressed.RemoveAllListeners();
            friendsButton?.OnButtonPressed.RemoveAllListeners();
            settingsButton?.OnButtonPressed.RemoveAllListeners();
            optionsButton?.OnButtonPressed.RemoveAllListeners();
            exitButton?.OnButtonPressed.RemoveAllListeners();
        }
    }
}
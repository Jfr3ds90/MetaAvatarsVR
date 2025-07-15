using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using Meta.XR;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Panel principal del menú VR con opciones principales
    /// </summary>
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
        
        protected override void SetupPanel()
        {
            base.SetupPanel();
            
            _dataManager = PlayerDataManager.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            UpdatePlayerInfo();
            
            AnimateLogo();
        }

        protected override void ConfigureButtons()
        {
            base.ConfigureButtons();
            
            if (playButton != null)
            {
                playButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.LobbyBrowser);
                });
            }
            
            if (hostButton != null)
            {
                hostButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.CreateLobby);
                });
            }
            
            if (friendsButton != null)
            {
                friendsButton.OnButtonPressed.AddListener(() =>
                {
                    _uiManager.ShowPanel(PanelID.Friends);
                });
            }
            
            if (settingsButton != null)
            {
                settingsButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.Settings);
                });
            }
            
            if (optionsButton != null)
            {
                optionsButton.OnButtonPressed.AddListener(() =>
                {
                    _uiManager.ShowPanel(PanelID.Options);
                });
            }
            
            if (exitButton != null)
            {
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
            
            logoTransform.DORotate(new Vector3(0, 360, 0), 20f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
            
            logoTransform.DOLocalMoveY(logoTransform.localPosition.y + 0.1f, 2f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            
            if (logoParticles != null)
            {
                logoParticles.Play();
            }
        }
        
        public override void OnPanelShown()
        {
            base.OnPanelShown();
            
            UpdateConnectionStatus();
            
            if (_networkBootstrapper != null)
            {
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
            // TODO: Implementar NotificationManager
            Debug.Log($"[Notification] {type}: {message}");
        }
        
        private enum NotificationType
        {
            Success,
            Error,
            Warning,
            Info
        }
    }
}
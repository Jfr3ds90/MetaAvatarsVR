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
        [SerializeField] private InteractableButton3D exitButton;
        
        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        
        [Header("Logo & Title")]
        [SerializeField] private Transform logoTransform;
        [SerializeField] private TextMeshProUGUI gameTitle;
        [SerializeField] private ParticleSystem logoParticles;
        
        private PlayerPrefsManager _prefsManager;
        private NetworkBootstrapper _networkBootstrapper;
        
        protected override void SetupPanel()
        {
            base.SetupPanel();
            
            // Obtener referencias
            _prefsManager = PlayerPrefsManager.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            // Configurar botones
            ConfigureButtons();
            
            // Actualizar información del jugador
            UpdatePlayerInfo();
            
            // Animar logo
            AnimateLogo();
        }
        
        private void ConfigureButtons()
        {
            // Botón Play - Buscar partidas
            if (playButton != null)
            {
                playButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.LobbyBrowser);
                });
            }
            
            // Botón Host - Crear sala
            if (hostButton != null)
            {
                hostButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.CreateRoom);
                });
            }
            
            // Botón Friends
            if (friendsButton != null)
            {
                friendsButton.OnButtonPressed.AddListener(() =>
                {
                    _uiManager.ShowPanel(PanelID.Friends);
                });
            }
            
            // Botón Settings
            if (settingsButton != null)
            {
                settingsButton.OnButtonPressed.AddListener(() => 
                {
                    _uiManager.ShowPanel(PanelID.Settings);
                });
            }
            
            // Botón Exit
            if (exitButton != null)
            {
                exitButton.OnButtonPressed.AddListener(() => 
                {
                    ShowExitConfirmation();
                });
            }
        }
        
        private void UpdatePlayerInfo()
        {
            // Nombre del jugador
            if (playerNameText != null && _prefsManager != null)
            {
                string playerName = _prefsManager.GetPlayerName();
                playerNameText.text = string.IsNullOrEmpty(playerName) ? "Guest Player" : playerName;
            }
            
            // Estado de conexión
            UpdateConnectionStatus();
            
            // Avatar (placeholder por ahora)
            if (playerAvatar != null)
            {
                // Aquí se integraría con Meta Avatar SDK
                // Por ahora usar un color aleatorio como placeholder
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
                
                // Habilitar botones de red
                if (playButton != null) playButton.SetInteractable(true);
                if (hostButton != null) hostButton.SetInteractable(true);
            }
            else
            {
                connectionStatusText.text = "<color=red>● Connecting...</color>";
                
                // Deshabilitar botones de red
                if (playButton != null) playButton.SetInteractable(false);
                if (hostButton != null) hostButton.SetInteractable(false);
            }
        }
        
        private Color GetPlayerColor()
        {
            // Generar color basado en el nombre del jugador
            string playerName = _prefsManager?.GetPlayerName() ?? "Guest";
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
            
            // Rotación continua del logo
            logoTransform.DORotate(new Vector3(0, 360, 0), 20f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
            
            // Efecto de flotación
            logoTransform.DOLocalMoveY(logoTransform.localPosition.y + 0.1f, 2f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            
            // Activar partículas si existen
            if (logoParticles != null)
            {
                logoParticles.Play();
            }
        }
        
        public override void OnPanelShown()
        {
            base.OnPanelShown();
            
            // Actualizar estado de conexión
            UpdateConnectionStatus();
            
            // Suscribirse a eventos de conexión
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnConnectedToServer_event.AddListener(OnConnected);
                _networkBootstrapper.OnConnectionFailed.AddListener(OnConnectionFailed);
            }
            
            // Efecto de entrada del título
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
            
            // Desuscribirse de eventos
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnConnectedToServer_event.RemoveListener(OnConnected);
                _networkBootstrapper.OnConnectionFailed.RemoveListener(OnConnectionFailed);
            }
        }
        
        private void OnConnected()
        {
            UpdateConnectionStatus();
            
            // Mostrar notificación de conexión exitosa
            ShowNotification("Connected to server!", NotificationType.Success);
        }
        
        private void OnConnectionFailed(string error)
        {
            UpdateConnectionStatus();
            
            // Mostrar notificación de error
            ShowNotification($"Connection failed: {error}", NotificationType.Error);
        }
        
        private void ShowExitConfirmation()
        {
            // Aquí se mostraría un diálogo de confirmación
            // Por ahora, salir directamente
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
        
        private void ShowNotification(string message, NotificationType type)
        {
            // Sistema de notificaciones flotantes
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
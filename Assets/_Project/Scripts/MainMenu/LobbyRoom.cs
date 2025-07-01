using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fusion;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Panel del lobby donde se muestran los jugadores conectados y se gestiona el estado antes de iniciar partida
    /// </summary>
    public class LobbyRoom : MenuPanel
    {
        [Header("Room Info")] [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Image roomStatusIndicator;

        [Header("Players List")] [SerializeField]
        private Transform playersContainer;

        [SerializeField] private GameObject playerItemPrefab; // Prefab con LobbyPlayerItem
        [SerializeField] private ScrollRect playersScrollView;
        [SerializeField] private int maxVisiblePlayers = 4;

        [Header("Local Player Controls")] [SerializeField]
        private InteractableButton3D readyButton;

        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Image readyStatusIndicator;

        [Header("Host Controls")] [SerializeField]
        private GameObject hostControlsPanel;

        [SerializeField] private InteractableButton3D startGameButton;
        [SerializeField] private InteractableButton3D kickPlayerButton;
        [SerializeField] private InteractableSlider3D maxPlayersSlider;
        [SerializeField] private Toggle isOpenToggle;

        [Header("Room Settings")] [SerializeField]
        private GameObject roomSettingsPanel;

        [SerializeField] private InteractableButton3D settingsButton;
        [SerializeField] private InteractableSlider3D difficultySlider;
        [SerializeField] private Toggle privateRoomToggle;

        [Header("Status & Feedback")] [SerializeField]
        private TextMeshProUGUI statusText;

        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private ParticleSystem confettiEffect;

        [Header("Colors")] [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;
        [SerializeField] private Color hostColor = new Color(1f, 0.8f, 0f); // Gold
        [SerializeField] private Color localPlayerColor = Color.cyan;

        // Private state
        private LobbyManager _lobbyManager;
        private NetworkBootstrapper _networkBootstrapper;
        private List<LobbyPlayerItem> _playerItems = new List<LobbyPlayerItem>();
        private bool _isLocalPlayerReady = false;
        private bool _isHost = false;
        private LobbyPlayer _selectedPlayer; // Para kick functionality

        // Animation tweeners
        private Tween _statusTextTween;
        private Tween _readyButtonTween;

        protected override void SetupPanel()
        {
            base.SetupPanel();

            // Obtener referencias
            _lobbyManager = LobbyManager.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;

            // Configurar botones
            ConfigureLobbyButtons();

            // Inicializar pool de player items
            InitializePlayerItemPool();

            // Configurar estado inicial
            UpdateHostControls();
        }

        private void ConfigureLobbyButtons()
        {
            // Botón Ready/Unready
            if (readyButton != null)
            {
                readyButton.OnButtonPressed.AddListener(ToggleReady);
            }

            // Botón Start Game (solo host)
            if (startGameButton != null)
            {
                startGameButton.OnButtonPressed.AddListener(StartGame);
            }

            // Botón Settings
            if (settingsButton != null)
            {
                settingsButton.OnButtonPressed.AddListener(ToggleRoomSettings);
            }

            // Slider de max players (solo host)
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.OnValueChanged.AddListener(OnMaxPlayersChanged);
            }

            // Toggle room visibility
            if (isOpenToggle != null)
            {
                isOpenToggle.onValueChanged.AddListener(OnRoomOpenChanged);
            }

            // Room privacy toggle
            if (privateRoomToggle != null)
            {
                privateRoomToggle.onValueChanged.AddListener(OnPrivateRoomChanged);
            }

            // Difficulty slider
            if (difficultySlider != null)
            {
                difficultySlider.OnValueChanged.AddListener(OnDifficultyChanged);
            }
        }

        private void InitializePlayerItemPool()
        {
            // Pre-crear items de jugador para mejor performance
            for (int i = 0; i < maxVisiblePlayers; i++)
            {
                GameObject playerObj = Instantiate(playerItemPrefab, playersContainer);
                LobbyPlayerItem playerItem = playerObj.GetComponent<LobbyPlayerItem>();

                if (playerItem != null)
                {
                    playerItem.Initialize(OnPlayerItemClicked);
                    playerItem.gameObject.SetActive(false);
                    _playerItems.Add(playerItem);
                }
            }
        }

        public override void OnPanelShown()
        {
            base.OnPanelShown();

            // Suscribirse a eventos del LobbyManager
            if (_lobbyManager != null)
            {
                _lobbyManager.OnPlayerJoined.AddListener(OnPlayerJoined);
                _lobbyManager.OnPlayerLeft.AddListener(OnPlayerLeft);
                _lobbyManager.OnPlayerUpdated.AddListener(OnPlayerUpdated);
                _lobbyManager.OnPlayerCountChanged.AddListener(OnPlayerCountChanged);
                _lobbyManager.OnAllPlayersReady.AddListener(OnAllPlayersReadyChanged);
            }

            // Actualizar información de la sala
            UpdateRoomInfo();

            // Actualizar lista de jugadores
            RefreshPlayersList();

            // Verificar si somos el host
            UpdateHostControls();

            // Animación de entrada
            AnimateRoomEntry();
        }

        public override void OnPanelHidden()
        {
            base.OnPanelHidden();

            // Desuscribirse de eventos
            if (_lobbyManager != null)
            {
                _lobbyManager.OnPlayerJoined.RemoveListener(OnPlayerJoined);
                _lobbyManager.OnPlayerLeft.RemoveListener(OnPlayerLeft);
                _lobbyManager.OnPlayerUpdated.RemoveListener(OnPlayerUpdated);
                _lobbyManager.OnPlayerCountChanged.RemoveListener(OnPlayerCountChanged);
                _lobbyManager.OnAllPlayersReady.RemoveListener(OnAllPlayersReadyChanged);
            }
        }

        #region Event Handlers

        private void OnPlayerJoined(LobbyPlayer player)
        {
            Debug.Log($"[LobbyRoom] Player joined: {player.GetDisplayName()}");

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} joined the lobby", MessageType.Info);

            // Efecto visual de entrada
            PlayJoinEffect();
        }

        private void OnPlayerLeft(LobbyPlayer player)
        {
            Debug.Log($"[LobbyRoom] Player left: {player.GetDisplayName()}");

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} left the lobby", MessageType.Warning);
        }

        private void OnPlayerUpdated(LobbyPlayer player)
        {
            // Actualizar solo el item específico del jugador
            var playerItem = _playerItems.FirstOrDefault(item =>
                item.gameObject.activeSelf && item.GetPlayerRef() == player.PlayerRef);

            if (playerItem != null)
            {
                playerItem.UpdatePlayerData(player);
            }

            // Si es el jugador local, actualizar controles
            if (player.IsLocalPlayer)
            {
                UpdateLocalPlayerControls(player);
            }

            // Actualizar contador y botón de start
            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void OnPlayerCountChanged(int current, int max)
        {
            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void OnAllPlayersReadyChanged(bool allReady)
        {
            UpdateStartButton();

            if (allReady && _lobbyManager.PlayerCount > 1)
            {
                ShowStatusMessage("All players ready! Host can start the game.", MessageType.Success);

                // Efecto de confetti si todos están listos
                if (confettiEffect != null)
                {
                    confettiEffect.Play();
                }
            }
        }

        #endregion

        #region UI Updates

        private void UpdateRoomInfo()
        {
            if (_networkBootstrapper == null) return;

            // Nombre de la sala
            if (roomNameText != null)
            {
                roomNameText.text = _networkBootstrapper.CurrentRoomName ?? "Room";
            }

            // Código de sala (simplificado)
            if (roomCodeText != null)
            {
                string roomCode = _networkBootstrapper.CurrentRoomName?.GetHashCode().ToString("X6") ?? "------";
                roomCodeText.text = $"#{roomCode}";
            }

            // Estado de la sala
            if (roomStatusIndicator != null)
            {
                roomStatusIndicator.color = _networkBootstrapper.IsInRoom ? readyColor : notReadyColor;
            }
        }

        private void RefreshPlayersList()
        {
            if (_lobbyManager == null) return;

            // Ocultar todos los items
            foreach (var item in _playerItems)
            {
                item.gameObject.SetActive(false);
            }

            // Mostrar jugadores actuales
            var players = _lobbyManager.Players.Values.ToList();

            for (int i = 0; i < players.Count && i < _playerItems.Count; i++)
            {
                LobbyPlayerItem item = _playerItems[i];
                LobbyPlayer player = players[i];

                item.UpdatePlayerData(player);
                item.gameObject.SetActive(true);

                // Posicionar con animación escalonada
                float targetY = -i * 0.15f; // Espaciado entre items
                item.transform.localPosition = new Vector3(0, targetY + 0.3f, 0);
                item.transform.DOLocalMoveY(targetY, 0.4f)
                    .SetDelay(i * 0.1f)
                    .SetEase(Ease.OutBack);
            }

            UpdatePlayerCount();
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText != null && _lobbyManager != null)
            {
                int current = _lobbyManager.PlayerCount;
                int max = _lobbyManager.MaxPlayers;

                playerCountText.text = $"Players: {current}/{max}";

                // Cambiar color según ocupación
                if (current == max)
                    playerCountText.color = notReadyColor;
                else if (current > max * 0.75f)
                    playerCountText.color = Color.yellow;
                else
                    playerCountText.color = readyColor;
            }
        }

        private void UpdateLocalPlayerControls(LobbyPlayer localPlayer)
        {
            _isLocalPlayerReady = localPlayer.IsReady;

            // Actualizar botón ready
            if (readyButton != null && readyButtonText != null)
            {
                readyButtonText.text = _isLocalPlayerReady ? "Not Ready" : "Ready";

                // Cambiar material del botón
                Color buttonColor = _isLocalPlayerReady ? readyColor : notReadyColor;
                // Aquí podrías cambiar el material del botón según el estado
            }

            // Actualizar indicador
            if (readyStatusIndicator != null)
            {
                readyStatusIndicator.color = _isLocalPlayerReady ? readyColor : notReadyColor;

                // Animación de pulso cuando está ready
                if (_isLocalPlayerReady)
                {
                    readyStatusIndicator.transform.DOScale(1.2f, 0.5f)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine);
                }
                else
                {
                    readyStatusIndicator.transform.DOKill();
                    readyStatusIndicator.transform.localScale = Vector3.one;
                }
            }
        }

        private void UpdateHostControls()
        {
            _isHost = _networkBootstrapper != null && _networkBootstrapper.IsHost;

            // Mostrar/ocultar controles de host
            if (hostControlsPanel != null)
            {
                hostControlsPanel.SetActive(_isHost);
            }

            // Configurar controles si somos host
            if (_isHost)
            {
                // Configurar slider de max players
                if (maxPlayersSlider != null && _lobbyManager != null)
                {
                    maxPlayersSlider.SetMinMax(2, 8);
                    maxPlayersSlider.SetValue(_lobbyManager.MaxPlayers);
                }
            }

            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            if (startGameButton == null || _lobbyManager == null) return;

            bool canStart = _isHost &&
                            _lobbyManager.AllPlayersReady &&
                            _lobbyManager.PlayerCount >= 2;

            startGameButton.SetInteractable(canStart);

            // Efecto visual en el botón cuando puede iniciar
            if (canStart)
            {
                startGameButton.transform.DOScale(1.1f, 0.8f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
            else
            {
                startGameButton.transform.DOKill();
                startGameButton.transform.localScale = Vector3.one;
            }
        }

        #endregion

        #region Button Actions

        private void ToggleReady()
        {
            if (_lobbyManager != null)
            {
                _lobbyManager.ToggleLocalPlayerReady();

                // Feedback háptico
                OVRInput.SetControllerVibration(1, 0.3f, OVRInput.Controller.Touch);
                DOVirtual.DelayedCall(0.1f, () => OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.Touch));

                // Efecto visual del botón
                if (_readyButtonTween != null) _readyButtonTween.Kill();
                _readyButtonTween = readyButton.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
            }
        }

        private void StartGame()
        {
            if (!_isHost || _lobbyManager == null) return;

            ShowStatusMessage("Starting game...", MessageType.Info);

            // Deshabilitar botón temporalmente
            if (startGameButton != null)
                startGameButton.SetInteractable(false);

            // Iniciar partida
            _lobbyManager.StartGame();
        }

        private void ToggleRoomSettings()
        {
            if (roomSettingsPanel != null)
            {
                bool isActive = roomSettingsPanel.activeSelf;
                roomSettingsPanel.SetActive(!isActive);

                // Animación de panel
                if (!isActive)
                {
                    roomSettingsPanel.transform.localScale = Vector3.zero;
                    roomSettingsPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                }
            }
        }

        private void OnPlayerItemClicked(LobbyPlayer player)
        {
            _selectedPlayer = player;

            // Solo el host puede kickear jugadores (excepto a sí mismo)
            if (_isHost && !player.IsLocalPlayer && kickPlayerButton != null)
            {
                kickPlayerButton.SetInteractable(true);
                ShowStatusMessage($"Selected: {player.GetDisplayName()}", MessageType.Info);
            }
        }

        #endregion

        #region Host Controls

        private void OnMaxPlayersChanged(float value)
        {
            if (!_isHost) return;

            int maxPlayers = Mathf.RoundToInt(value);
            // TODO: Implementar RPC para cambiar max players
            Debug.Log($"[LobbyRoom] Max players changed to: {maxPlayers}");
        }

        private void OnRoomOpenChanged(bool isOpen)
        {
            if (!_isHost) return;

            // TODO: Implementar RPC para cambiar visibilidad de sala
            Debug.Log($"[LobbyRoom] Room open changed to: {isOpen}");
        }

        private void OnPrivateRoomChanged(bool isPrivate)
        {
            if (!_isHost) return;

            // TODO: Implementar RPC para cambiar privacidad
            Debug.Log($"[LobbyRoom] Private room changed to: {isPrivate}");
        }

        private void OnDifficultyChanged(float value)
        {
            if (!_isHost) return;

            // TODO: Implementar configuración de dificultad
            Debug.Log($"[LobbyRoom] Difficulty changed to: {value}");
        }

        #endregion

        #region Visual Effects & Feedback

        private void AnimateRoomEntry()
        {
            // Animación de entrada del panel completo
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

            // Animar elementos secuencialmente
            if (roomNameText != null)
            {
                roomNameText.transform.localScale = Vector3.zero;
                roomNameText.transform.DOScale(Vector3.one, 0.3f)
                    .SetDelay(0.2f)
                    .SetEase(Ease.OutBack);
            }
        }

        private void PlayJoinEffect()
        {
            // Efecto de partículas o animación cuando alguien se une
            if (confettiEffect != null)
            {
                var emission = confettiEffect.emission;
                emission.SetBursts(new ParticleSystem.Burst[]
                {
                    new ParticleSystem.Burst(0, 20)
                });
                confettiEffect.Play();
            }
        }

        private void ShowStatusMessage(string message, MessageType type)
        {
            if (statusText == null) return;

            // Color según tipo de mensaje
            Color messageColor = type switch
            {
                MessageType.Success => readyColor,
                MessageType.Warning => Color.yellow,
                MessageType.Error => notReadyColor,
                _ => Color.white
            };

            statusText.text = message;
            statusText.color = messageColor;

            // Animación de fade
            if (_statusTextTween != null) _statusTextTween.Kill();
            statusText.alpha = 0f;
            _statusTextTween = statusText.DOFade(1f, 0.3f).OnComplete(() =>
            {
                DOVirtual.DelayedCall(3f, () => statusText.DOFade(0f, 0.5f));
            });
        }

        #endregion

        #region Utility

        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }

        protected override void ConfigureButtons()
        {
            base.ConfigureButtons();

            // El back button debería abandonar la sala
            if (backButton != null)
            {
                backButton.OnButtonPressed.RemoveAllListeners();
                backButton.OnButtonPressed.AddListener(LeaveRoom);
            }
        }

        private async void LeaveRoom()
        {
            if (_networkBootstrapper != null)
            {
                ShowStatusMessage("Leaving room...", MessageType.Info);
                await _networkBootstrapper.LeaveRoom();
                _uiManager.ShowPanel(PanelID.LobbyBrowser);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // Cleanup tweens
            _statusTextTween?.Kill();
            _readyButtonTween?.Kill();
        }
    }
}
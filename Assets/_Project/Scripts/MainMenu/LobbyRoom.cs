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
    /// LobbyRoom ACTUALIZADO - Usa LobbyState + LobbyController (arquitectura limpia)
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

        // ✅ CAMBIO: Referencias a nuevos componentes
        private LobbyState _lobbyState;
        private LobbyController _lobbyController;
        private List<LobbyPlayerItem> _playerItems = new List<LobbyPlayerItem>();
        private bool _isLocalPlayerReady = false;
        private LobbyPlayer _selectedPlayer; // Para kick functionality

        // Animation tweeners
        private Tween _statusTextTween;
        private Tween _readyButtonTween;

        protected override void SetupPanel()
        {
            base.SetupPanel();

            Debug.Log("🧪 [LOBBYROOM] Setting up panel...");

            // ✅ CAMBIO: Obtener nuevas referencias
            _lobbyState = LobbyState.Instance;
            _lobbyController = LobbyController.Instance;

            // Debug de referencias
            Debug.Log($"🧪 [LOBBYROOM] LobbyState: {_lobbyState != null}");
            Debug.Log($"🧪 [LOBBYROOM] LobbyController: {_lobbyController != null}");

            if (_lobbyState == null)
            {
                Debug.LogError("🧪 [LOBBYROOM] ❌ LobbyState.Instance is NULL!");
            }

            if (_lobbyController == null)
            {
                Debug.LogError("🧪 [LOBBYROOM] ❌ LobbyController.Instance is NULL!");
            }

            ConfigureLobbyButtons();
            InitializePlayerItemPool();
            UpdateHostControls();

            Debug.Log("🧪 [LOBBYROOM] ✅ Panel setup completed");
        }

        private void ConfigureLobbyButtons()
        {
            Debug.Log("🧪 [LOBBYROOM] Configuring lobby buttons...");

            // ✅ CAMBIO: Usar LobbyController para acciones
            if (readyButton != null)
            {
                readyButton.OnButtonPressed.AddListener(() =>
                {
                    Debug.Log("🧪 [LOBBYROOM] Ready button pressed");
                    _lobbyController?.ToggleReady();
                });
            }

            if (startGameButton != null)
            {
                startGameButton.OnButtonPressed.AddListener(() =>
                {
                    Debug.Log("🧪 [LOBBYROOM] Start game button pressed");
                    _lobbyController?.StartGame();
                });
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

            Debug.Log("🧪 [LOBBYROOM] ✅ Buttons configured");
        }

        private void InitializePlayerItemPool()
        {
            Debug.Log("🧪 [LOBBYROOM] Initializing player item pool...");

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

            Debug.Log($"🧪 [LOBBYROOM] ✅ Created {_playerItems.Count} player items");
        }

        public override void OnPanelShown()
        {
            base.OnPanelShown();

            Debug.Log("🧪 [LOBBYROOM] Panel shown, setting up events...");

            // Suscribirse a LobbyState
            if (_lobbyState != null)
            {
                _lobbyState.OnPlayerJoined.AddListener(OnPlayerJoined);
                _lobbyState.OnPlayerLeft.AddListener(OnPlayerLeft);
                _lobbyState.OnPlayerUpdated.AddListener(OnPlayerUpdated);
                _lobbyState.OnPlayerCountChanged.AddListener(OnPlayerCountChanged);
                _lobbyState.OnAllPlayersReady.AddListener(OnAllPlayersReadyChanged);

                Debug.Log("🧪 [LOBBYROOM] ✅ Subscribed to LobbyState events");
            }
            else
            {
                Debug.LogError("🧪 [LOBBYROOM] ❌ Cannot subscribe to events - LobbyState is null");
            }

            // Suscribirse a eventos del controller
            if (_lobbyController != null)
            {
                _lobbyController.OnGameStarting.AddListener(() =>
                {
                    ShowStatusMessage("Starting game...", MessageType.Info);
                });

                _lobbyController.OnGameStartFailed.AddListener(() =>
                {
                    ShowStatusMessage("Failed to start game", MessageType.Error);
                });

                _lobbyController.OnActionFailed.AddListener((error) =>
                {
                    ShowStatusMessage(error, MessageType.Error);
                });

                Debug.Log("🧪 [LOBBYROOM] ✅ Subscribed to LobbyController events");
            }

            // Actualizar información de la sala
            UpdateRoomInfo();

            // CORRECCIÓN CRÍTICA: Esperar un frame antes de refrescar
            // Esto asegura que LobbyPlayer ya se haya registrado
            StartCoroutine(DelayedRefresh());

            // Verificar si somos el host
            UpdateHostControls();

            // Animación de entrada
            AnimateRoomEntry();

            Debug.Log("🧪 [LOBBYROOM] ✅ Panel fully initialized");
        }

        public override void OnPanelHidden()
        {
            base.OnPanelHidden();

            Debug.Log("🧪 [LOBBYROOM] Panel hidden, cleaning up events...");

            // ✅ CAMBIO: Desuscribirse de LobbyState
            if (_lobbyState != null)
            {
                _lobbyState.OnPlayerJoined.RemoveListener(OnPlayerJoined);
                _lobbyState.OnPlayerLeft.RemoveListener(OnPlayerLeft);
                _lobbyState.OnPlayerUpdated.RemoveListener(OnPlayerUpdated);
                _lobbyState.OnPlayerCountChanged.RemoveListener(OnPlayerCountChanged);
                _lobbyState.OnAllPlayersReady.RemoveListener(OnAllPlayersReadyChanged);

                Debug.Log("🧪 [LOBBYROOM] ✅ Unsubscribed from LobbyState events");
            }

            // Desuscribirse del controller
            if (_lobbyController != null)
            {
                _lobbyController.OnGameStarting.RemoveAllListeners();
                _lobbyController.OnGameStartFailed.RemoveAllListeners();
                _lobbyController.OnActionFailed.RemoveAllListeners();

                Debug.Log("🧪 [LOBBYROOM] ✅ Unsubscribed from LobbyController events");
            }
        }

        private System.Collections.IEnumerator DelayedRefresh()
        {
            Debug.Log("🧪 [LOBBYROOM] Waiting before refresh...");

            // Esperar 2 frames para asegurar que todo esté inicializado
            yield return null;
            yield return null;

            // Ahora sí refrescar
            Debug.Log("🧪 [LOBBYROOM] Executing delayed refresh...");
            RefreshPlayersList();

            // Si aún no hay jugadores, intentar de nuevo
            if (_lobbyState != null && _lobbyState.PlayerCount == 0)
            {
                Debug.LogWarning("🧪 [LOBBYROOM] No players found, retrying in 0.5s...");
                yield return new WaitForSeconds(0.5f);
                RefreshPlayersList();
            }
        }

        #region Event Handlers

        private void OnPlayerJoined(LobbyPlayer player)
        {
            Debug.Log($"🧪 [LOBBYROOM] 🎉 Player joined: {player.GetDisplayName()}");

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} joined the lobby", MessageType.Info);

            // Efecto visual de entrada
            PlayJoinEffect();
        }

        private void OnPlayerLeft(LobbyPlayer player)
        {
            Debug.Log($"🧪 [LOBBYROOM] 👋 Player left: {player.GetDisplayName()}");

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} left the lobby", MessageType.Warning);
        }

        private void OnPlayerUpdated(LobbyPlayer player)
        {
            Debug.Log($"🧪 [LOBBYROOM] 🔄 Player updated: {player.GetDisplayName()} - Ready: {player.IsReady}");

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
            Debug.Log($"🧪 [LOBBYROOM] 📊 Player count changed: {current}/{max}");

            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void OnAllPlayersReadyChanged(bool allReady)
        {
            Debug.Log($"🧪 [LOBBYROOM] 🎯 All players ready changed: {allReady}");

            UpdateStartButton();

            if (allReady && _lobbyState != null && _lobbyState.PlayerCount > 1)
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
            Debug.Log("🧪 [LOBBYROOM] Updating room info...");

            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo == null)
            {
                Debug.LogWarning("🧪 [LOBBYROOM] ⚠️ Cannot update room info - LobbyController returned null");
                return;
            }

            // Nombre de la sala
            if (roomNameText != null)
            {
                roomNameText.text = lobbyInfo.RoomName ?? "Room";
            }

            // Código de sala (simplificado)
            if (roomCodeText != null)
            {
                roomCodeText.text = $"#{lobbyInfo.RoomCode}";
            }

            // Estado de la sala
            if (roomStatusIndicator != null)
            {
                roomStatusIndicator.color = lobbyInfo.IsInLobby ? readyColor : notReadyColor;
            }

            Debug.Log($"🧪 [LOBBYROOM] ✅ Room info updated - {lobbyInfo.RoomName}");
        }

        private void RefreshPlayersList()
        {
            if (_lobbyState == null)
            {
                Debug.LogWarning("🧪 [LOBBYROOM] ⚠️ Cannot refresh players list - LobbyState is null");
                return;
            }

            Debug.Log("🧪 [LOBBYROOM] Refreshing players list...");

            // Ocultar todos los items primero
            foreach (var item in _playerItems)
            {
                item.gameObject.SetActive(false);
            }

            // Obtener jugadores REALES de la red
            var players = _lobbyState.GetPlayersList(hostFirst: true);

            Debug.Log($"🧪 [LOBBYROOM] Found {players.Count} players to display");

            // CORRECCIÓN CRÍTICA: Verificar que tenemos jugadores reales
            if (players.Count == 0)
            {
                Debug.LogWarning("🧪 [LOBBYROOM] ⚠️ No players found in LobbyState!");
                return;
            }

            for (int i = 0; i < players.Count && i < _playerItems.Count; i++)
            {
                LobbyPlayerItem item = _playerItems[i];
                LobbyPlayer player = players[i];

                // CORRECCIÓN: Verificar que el player no sea null
                if (player == null)
                {
                    Debug.LogError($"🧪 [LOBBYROOM] ❌ Player at index {i} is null!");
                    continue;
                }

                // DEBUG: Log detallado del jugador
                Debug.Log($"🧪 [LOBBYROOM] Displaying player {i}:");
                Debug.Log($"  - Name: {player.PlayerName.ToString()}");
                Debug.Log($"  - Display Name: {player.GetDisplayName()}");
                Debug.Log($"  - Is Host: {player.IsHost}");
                Debug.Log($"  - Is Local: {player.IsLocalPlayer}");
                Debug.Log($"  - Is Ready: {player.IsReady}");
                Debug.Log($"  - Player Ref: {player.PlayerRef}");

                // Actualizar el item con datos del jugador real
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

            Debug.Log("🧪 [LOBBYROOM] ✅ Players list refreshed");
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText == null) return;

            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo == null) return;

            playerCountText.text = $"Players: {lobbyInfo.CurrentPlayers}/{lobbyInfo.MaxPlayers}";

            // Cambiar color según ocupación
            if (lobbyInfo.CurrentPlayers == lobbyInfo.MaxPlayers)
                playerCountText.color = notReadyColor;
            else if (lobbyInfo.CurrentPlayers > lobbyInfo.MaxPlayers * 0.75f)
                playerCountText.color = Color.yellow;
            else
                playerCountText.color = readyColor;
        }

        private void UpdateLocalPlayerControls(LobbyPlayer localPlayer)
        {
            _isLocalPlayerReady = localPlayer.IsReady;

            Debug.Log($"🧪 [LOBBYROOM] Updating local player controls - Ready: {_isLocalPlayerReady}");

            // Actualizar botón ready
            if (readyButton != null && readyButtonText != null)
            {
                readyButtonText.text = _isLocalPlayerReady ? "Not Ready" : "Ready";
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
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            bool isHost = lobbyInfo?.IsHost ?? false;

            Debug.Log($"🧪 [LOBBYROOM] Updating host controls - Is Host: {isHost}");

            // Mostrar/ocultar controles de host
            if (hostControlsPanel != null)
            {
                hostControlsPanel.SetActive(isHost);
            }

            // Configurar controles si somos host
            if (isHost && lobbyInfo != null)
            {
                // Configurar slider de max players
                if (maxPlayersSlider != null)
                {
                    maxPlayersSlider.SetMinMax(2, 8);
                    maxPlayersSlider.SetValue(lobbyInfo.MaxPlayers);
                }
            }

            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            if (startGameButton == null || _lobbyController == null) return;

            bool canStart = _lobbyController.CanStartGame;
            startGameButton.SetInteractable(canStart);

            Debug.Log($"🧪 [LOBBYROOM] Start button - Can start: {canStart}");

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

            Debug.Log($"🧪 [LOBBYROOM] Player item clicked: {player.GetDisplayName()}");

            // Solo el host puede kickear jugadores (excepto a sí mismo)
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost == true && !player.IsLocalPlayer && kickPlayerButton != null)
            {
                kickPlayerButton.SetInteractable(true);
                ShowStatusMessage($"Selected: {player.GetDisplayName()}", MessageType.Info);
            }
        }

        #endregion

        #region Host Controls

        private void OnMaxPlayersChanged(float value)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            int maxPlayers = Mathf.RoundToInt(value);
            // TODO: Implementar RPC para cambiar max players
            Debug.Log($"🧪 [LOBBYROOM] Max players changed to: {maxPlayers}");
        }

        private void OnRoomOpenChanged(bool isOpen)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar RPC para cambiar visibilidad de sala
            Debug.Log($"🧪 [LOBBYROOM] Room open changed to: {isOpen}");
        }

        private void OnPrivateRoomChanged(bool isPrivate)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar RPC para cambiar privacidad
            Debug.Log($"🧪 [LOBBYROOM] Private room changed to: {isPrivate}");
        }

        private void OnDifficultyChanged(float value)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar configuración de dificultad
            Debug.Log($"🧪 [LOBBYROOM] Difficulty changed to: {value}");
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

            // ✅ CAMBIO: El back button usa LobbyController
            if (backButton != null)
            {
                backButton.OnButtonPressed.RemoveAllListeners();
                backButton.OnButtonPressed.AddListener(async () =>
                {
                    ShowStatusMessage("Leaving room...", MessageType.Info);
                    _lobbyController?.LeaveLobby();

                    // Pequeño delay para que se procese la salida
                    await System.Threading.Tasks.Task.Delay(500);
                    _uiManager.ShowPanel(PanelID.LobbyBrowser);
                });
            }
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Debug: Room Status")]
        private void DebugRoomStatus()
        {
            Debug.Log("=== LobbyRoom Debug Status ===");
            Debug.Log($"LobbyState: {_lobbyState != null}");
            Debug.Log($"LobbyController: {_lobbyController != null}");

            if (_lobbyState != null)
            {
                var stats = _lobbyState.GetLobbyStats();
                Debug.Log($"Players: {stats.TotalPlayers}/{stats.MaxPlayers}");
                Debug.Log($"Ready: {stats.ReadyPlayers}/{stats.TotalPlayers}");
                Debug.Log($"All Ready: {stats.AllReady}");
                Debug.Log($"Host: {stats.HostName}");
                Debug.Log($"Local: {stats.LocalPlayerName}");
            }

            if (_lobbyController != null)
            {
                var info = _lobbyController.GetLobbyInfo();
                if (info != null)
                {
                    Debug.Log($"Room: {info.RoomName}");
                    Debug.Log($"Is Host: {info.IsHost}");
                    Debug.Log($"Can Start: {info.CanStart}");
                    Debug.Log($"Status: {info.StatusText}");
                }
            }

            Debug.Log("================================");
        }

        [ContextMenu("Debug: Force Refresh")]
        private void DebugForceRefresh()
        {
            Debug.Log("🧪 [DEBUG] Forcing refresh...");
            RefreshPlayersList();
            UpdateRoomInfo();
            UpdateHostControls();
        }

        [ContextMenu("Debug: Force Refresh Players")]
        private void DebugForceRefreshPlayers()
        {
            Debug.Log("🧪 [DEBUG] === FORCE REFRESH PLAYERS ===");

            if (_lobbyState == null)
            {
                Debug.LogError("🧪 [DEBUG] LobbyState is null!");
                _lobbyState = LobbyState.Instance;
                if (_lobbyState == null)
                {
                    Debug.LogError("🧪 [DEBUG] LobbyState.Instance is also null!");
                    return;
                }
            }

            var players = _lobbyState.GetPlayersList(hostFirst: true);
            Debug.Log($"🧪 [DEBUG] Players in LobbyState: {players.Count}");

            foreach (var player in players)
            {
                if (player != null)
                {
                    Debug.Log($"🧪 [DEBUG] - {player.GetDisplayName()} (Ref: {player.PlayerRef})");
                }
                else
                {
                    Debug.LogError("🧪 [DEBUG] - NULL PLAYER!");
                }
            }

            RefreshPlayersList();

            Debug.Log("🧪 [DEBUG] === END FORCE REFRESH ===");
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
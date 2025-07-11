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
        [Header("Room Info")] 
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private Image roomStatusIndicator;

        [Header("Players List")] 
        [SerializeField] private Transform playersContainer;
        [SerializeField] private GameObject playerItemPrefab; // Prefab con LobbyPlayerItem
        [SerializeField] private ScrollRect playersScrollView;
        [SerializeField] private int maxVisiblePlayers = 4;

        [Header("Local Player Controls")] 
        [SerializeField] private InteractableButton3D readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private Image readyStatusIndicator;

        [Header("Host Controls")] 
        [SerializeField] private GameObject hostControlsPanel;
        [SerializeField] private InteractableButton3D startGameButton;
        [SerializeField] private InteractableButton3D kickPlayerButton;
        [SerializeField] private InteractableSlider3D maxPlayersSlider;
        [SerializeField] private Toggle isOpenToggle;
        
        [Header("Map Selection (Host Only)")]
        [SerializeField] private GameObject mapSelectionPanel;
        [SerializeField] private Transform mapButtonsContainer;
        [SerializeField] private GameObject mapButtonPrefab;
        [SerializeField] private TextMeshProUGUI currentMapText;
        [SerializeField] private Image currentMapPreview;
        [SerializeField] private InteractableButton3D changeMapButton;

        private string _selectedMapName;
        private List<GameObject> _mapButtons = new List<GameObject>();

        [Header("Room Settings")] 
        [SerializeField] private GameObject roomSettingsPanel;
        [SerializeField] private InteractableButton3D settingsButton;
        [SerializeField] private InteractableSlider3D difficultySlider;
        [SerializeField] private Toggle privateRoomToggle;

        [Header("Status & Feedback")] 
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private ParticleSystem confettiEffect;

        [Header("Colors")] 
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;
        [SerializeField] private Color hostColor = new Color(1f, 0.8f, 0f); // Gold
        [SerializeField] private Color localPlayerColor = Color.cyan;

        // Referencias a nuevos componentes
        private LobbyState _lobbyState;
        private LobbyController _lobbyController;
        private NetworkBootstrapper _networkBootstrapper;
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

            _lobbyState = LobbyState.Instance;
            _lobbyController = LobbyController.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;

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
                    StartGameWithSelectedMap();
                });
            }
            
            if (changeMapButton != null)
            {
                changeMapButton.OnButtonPressed.AddListener(ToggleMapSelection);
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
                _lobbyState.OnMapChanged.AddListener(OnMapChangedByHost);


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
                _lobbyState.OnMapChanged.RemoveListener(OnMapChangedByHost);

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

        #region  Map Selection

        private void StartGameWithSelectedMap()
        {
            if (_lobbyController == null || _networkBootstrapper == null)
            {
                Debug.LogError("🧪 [LOBBYROOM] Controller or Bootstrapper not available");
                return;
            }
    
            // Establecer el mapa seleccionado antes de iniciar
            if (!string.IsNullOrEmpty(_selectedMapName))
            {
                _networkBootstrapper.SelectedSceneName = _selectedMapName;
            }
    
            // Iniciar el juego
            _lobbyController.StartGame();
        }
        
        private void ToggleMapSelection()
        {
            if (mapSelectionPanel == null) return;
    
            bool isActive = mapSelectionPanel.activeSelf;
            mapSelectionPanel.SetActive(!isActive);
    
            if (!isActive)
            {
                // Mostrar mapas disponibles
                PopulateMapSelection();
        
                // Animación de entrada
                mapSelectionPanel.transform.localScale = Vector3.zero;
                mapSelectionPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }
        
        private void PopulateMapSelection()
        {
            if (_networkBootstrapper == null || mapButtonsContainer == null || mapButtonPrefab == null)
                return;
        
            // Limpiar botones anteriores
            foreach (var btn in _mapButtons)
            {
                if (btn != null) Destroy(btn);
            }
            _mapButtons.Clear();
    
            // Obtener mapas disponibles
            var availableScenes = _networkBootstrapper.GetAvailableScenes();
    
            Debug.Log($"🧪 [LOBBYROOM] Populating {availableScenes.Count} maps");
    
            // Crear botón para cada mapa
            for (int i = 0; i < availableScenes.Count; i++)
            {
                var sceneInfo = availableScenes[i];
                GameObject mapBtn = Instantiate(mapButtonPrefab, mapButtonsContainer);
        
                // Configurar visual del botón
                var nameText = mapBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = sceneInfo.displayName;
                }
        
                // Configurar imagen preview si existe
                var previewImage = mapBtn.GetComponentInChildren<Image>();
                if (previewImage != null && sceneInfo.previewImage != null)
                {
                    previewImage.sprite = sceneInfo.previewImage;
                }
        
                // Configurar botón
                var button = mapBtn.GetComponent<InteractableButton3D>();
                if (button != null)
                {
                    string sceneName = sceneInfo.sceneName; // Captura para closure
                    button.OnButtonPressed.AddListener(() => SelectMap(sceneName));
                }
        
                _mapButtons.Add(mapBtn);
        
                // Animación escalonada
                mapBtn.transform.localScale = Vector3.zero;
                mapBtn.transform.DOScale(Vector3.one, 0.3f)
                    .SetDelay(i * 0.1f)
                    .SetEase(Ease.OutBack);
            }
        }
        
        private void SelectMap(string mapName)
        {
            Debug.Log($"🧪 [LOBBYROOM] === SELECT MAP START ===");
            Debug.Log($"🧪 [LOBBYROOM] Map to select: {mapName}");
    
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            Debug.Log($"🧪 [LOBBYROOM] Is Host: {lobbyInfo?.IsHost}");
    
            if (lobbyInfo?.IsHost != true)
            {
                ShowStatusMessage("Only host can change map", MessageType.Warning);
                Debug.Log($"🧪 [LOBBYROOM] Not host, cannot change map");
                return;
            }

            // Validar que el mapa existe
            if (!_networkBootstrapper.IsValidScene(mapName))
            {
                ShowStatusMessage("Invalid map selection", MessageType.Error);
                Debug.LogError($"🧪 [LOBBYROOM] Invalid scene: {mapName}");
                return;
            }

            _selectedMapName = mapName;
            Debug.Log($"🧪 [LOBBYROOM] Local map name set to: {_selectedMapName}");

            // Sincronizar con la red via RPC
            var localPlayer = _lobbyState?.LocalPlayer;
            Debug.Log($"🧪 [LOBBYROOM] Local player exists: {localPlayer != null}");
            Debug.Log($"🧪 [LOBBYROOM] Local player is host: {localPlayer?.IsHost}");
    
            if (localPlayer != null && localPlayer.IsHost)
            {
                Debug.Log($"🧪 [LOBBYROOM] 📡 Calling RPC_ChangeMap with: {mapName}");
                localPlayer.RPC_ChangeMap(mapName);
            }
            else
            {
                Debug.LogError($"🧪 [LOBBYROOM] ❌ Cannot call RPC - player not host or null");
            }

            // Actualizar UI localmente primero
            UpdateMapDisplay();

            // Cerrar panel de selección
            if (mapSelectionPanel != null)
            {
                mapSelectionPanel.transform.DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => mapSelectionPanel.SetActive(false));
            }

            ShowStatusMessage($"Map changed to: {mapName}", MessageType.Info);
            Debug.Log($"🧪 [LOBBYROOM] === SELECT MAP END ===");
        }

        
        private void OnMapChangedByHost(string newMapName)
        {
            if (string.IsNullOrEmpty(newMapName)) return;
    
            _selectedMapName = newMapName;
            UpdateMapDisplay();
    
            // Mostrar notificación solo si no somos el host
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost == false)
            {
                ShowStatusMessage($"Host changed map to: {newMapName}", MessageType.Info);
            }
        }

        #endregion

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

            try
            {
                // Ocultar todos los items primero
                foreach (var item in _playerItems)
                {
                    if (item != null && item.gameObject != null)
                        item.gameObject.SetActive(false);
                }

                // Obtener jugadores REALES de la red con manejo de errores
                List<LobbyPlayer> players = null;
                try
                {
                    players = _lobbyState.GetPlayersList(hostFirst: true);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"🧪 [LOBBYROOM] ❌ Error getting players list: {e.Message}");
                    return;
                }

                Debug.Log($"🧪 [LOBBYROOM] Found {players?.Count ?? 0} players to display");

                // Verificar que tenemos jugadores reales
                if (players == null || players.Count == 0)
                {
                    Debug.LogWarning("🧪 [LOBBYROOM] ⚠️ No players found in LobbyState!");
                    return;
                }

                // Mostrar jugadores
                for (int i = 0; i < players.Count && i < _playerItems.Count; i++)
                {
                    LobbyPlayerItem item = _playerItems[i];
                    LobbyPlayer player = players[i];

                    //Verificar que el player no sea null
                    if (player == null)
                    {
                        Debug.LogError($"🧪 [LOBBYROOM] ❌ Player at index {i} is null!");
                        continue;
                    }

                    //Log detallado del jugador
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
                    //float targetY = -i * 0.15f; // Espaciado entre items
                    //item.transform.localPosition = new Vector3(0, targetY + 0.3f, 0);

                    // Animación con manejo de errores
                    /*try
                    {
                        item.transform.DOLocalMoveY(targetY, 0.4f)
                            .SetDelay(i * 0.1f)
                            .SetEase(Ease.OutBack);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"🧪 [LOBBYROOM] Animation error: {e.Message}");
                        item.transform.localPosition = new Vector3(0, targetY, 0);
                    }*/
                }

                UpdatePlayerCount();
                Debug.Log("🧪 [LOBBYROOM] ✅ Players list refreshed successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"🧪 [LOBBYROOM] ❌ Critical error in RefreshPlayersList: {e.Message}\n{e.StackTrace}");

                // Intentar mostrar al menos algo
                ShowErrorInPlayerList("Error loading players");
            }
        }
        
        private void ShowErrorInPlayerList(string errorMessage)
        {
            // Mostrar al menos un item con el error
            if (_playerItems.Count > 0 && _playerItems[0] != null)
            {
                var firstItem = _playerItems[0];
                firstItem.gameObject.SetActive(true);
        
                // Buscar el text component y mostrar error
                var nameText = firstItem.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = errorMessage;
                    nameText.color = Color.red;
                }
            }
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
        
        private void UpdateMapDisplay()
        {
            if (_networkBootstrapper == null) return;
    
            string currentMap = !string.IsNullOrEmpty(_selectedMapName) 
                ? _selectedMapName 
                : _networkBootstrapper.SelectedSceneName;
        
            var sceneInfo = _networkBootstrapper.GetSceneInfo(currentMap);
    
            if (sceneInfo != null)
            {
                if (currentMapText != null)
                    currentMapText.text = sceneInfo.displayName;
            
                if (currentMapPreview != null && sceneInfo.previewImage != null)
                    currentMapPreview.sprite = sceneInfo.previewImage;
            }
            else
            {
                if (currentMapText != null)
                    currentMapText.text = "Default Map";
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
    
            // Mostrar/ocultar botón de cambiar mapa
            if (changeMapButton != null)
            {
                changeMapButton.gameObject.SetActive(isHost);
            }

            // Configurar controles si somos host
            if (isHost && lobbyInfo != null)
            {
                // Actualizar display del mapa
                UpdateMapDisplay();
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
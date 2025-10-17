using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Fusion;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using HackMonkeys.Debugging;
using LogLevel = HackMonkeys.Debugging.LogLevel;


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
        [SerializeField] private GameObject playerItemPrefab; 
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

        private LobbyState _lobbyState;
        private LobbyController _lobbyController;
        private NetworkBootstrapper _networkBootstrapper;
        private List<LobbyPlayerItem> _playerItems = new List<LobbyPlayerItem>();
        private bool _isLocalPlayerReady = false;
        private LobbyPlayer _selectedPlayer; // Para kick functionality

        private Tween _statusTextTween;
        private Tween _readyButtonTween;

        protected override void SetupPanel()
        {
            base.SetupPanel();

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Setting up panel...", LogCategory.Avatar, LogLevel.Debug);

            _lobbyState = LobbyState.Instance;
            _lobbyController = LobbyController.Instance;
            _networkBootstrapper = NetworkBootstrapper.Instance;

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] LobbyState: {_lobbyState != null}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] LobbyController: {_lobbyController != null}", LogCategory.Avatar, LogLevel.Debug);

            if (_lobbyState == null)
            {
                AdvancedDebugSystem.LogError("🧪 [LOBBYROOM] ❌ LobbyState.Instance is NULL!", LogCategory.Avatar);
            }

            if (_lobbyController == null)
            {
                AdvancedDebugSystem.LogError("🧪 [LOBBYROOM] ❌ LobbyController.Instance is NULL!", LogCategory.Avatar);
            }

            ConfigureLobbyButtons();
            InitializePlayerItemPool();
            UpdateHostControls();

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Panel setup completed", LogCategory.Avatar, LogLevel.Debug);
        }

        private void ConfigureLobbyButtons()
        {
            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Configuring lobby buttons...", LogCategory.Avatar, LogLevel.Debug);

            if (readyButton != null)
            {
                readyButton.OnButtonPressed.AddListener(() =>
                {
                    AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Ready button pressed", LogCategory.Avatar, LogLevel.Debug);
                    _lobbyController?.ToggleReady();
                });
            }

            if (startGameButton != null)
            {
                startGameButton.OnButtonPressed.AddListener(() =>
                {
                    AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Start game button pressed", LogCategory.Avatar, LogLevel.Debug);
                    StartGameWithSelectedMap();
                });
            }
    
            if (changeMapButton != null)
            {
                changeMapButton.OnButtonPressed.AddListener(ToggleMapSelection);
                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Change map button configured", LogCategory.Avatar, LogLevel.Debug);
            }

            if (settingsButton != null)
            {
                settingsButton.OnButtonPressed.AddListener(ToggleRoomSettings);
            }

            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.OnValueChanged.AddListener(OnMaxPlayersChanged);
            }

            if (isOpenToggle != null)
            {
                isOpenToggle.onValueChanged.AddListener(OnRoomOpenChanged);
            }

            if (privateRoomToggle != null)
            {
                privateRoomToggle.onValueChanged.AddListener(OnPrivateRoomChanged);
            }

            if (difficultySlider != null)
            {
                difficultySlider.OnValueChanged.AddListener(OnDifficultyChanged);
            }

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Buttons configured", LogCategory.Avatar, LogLevel.Debug);
        }

        private void InitializePlayerItemPool()
        {
            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Initializing player item pool...", LogCategory.Avatar, LogLevel.Debug);

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

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] ✅ Created {_playerItems.Count} player items", LogCategory.Avatar, LogLevel.Debug);
        }

        public override void OnPanelShown()
        {
            base.OnPanelShown();

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Panel shown, setting up events...", LogCategory.Avatar, LogLevel.Debug);

            if (_lobbyState != null)
            {
                _lobbyState.OnPlayerJoined.AddListener(OnPlayerJoined);
                _lobbyState.OnPlayerLeft.AddListener(OnPlayerLeft);
                _lobbyState.OnPlayerUpdated.AddListener(OnPlayerUpdated);
                _lobbyState.OnPlayerCountChanged.AddListener(OnPlayerCountChanged);
                _lobbyState.OnAllPlayersReady.AddListener(OnAllPlayersReadyChanged);
                _lobbyState.OnMapChanged.AddListener(OnMapChangedByHost);

                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Subscribed to LobbyState events", LogCategory.Avatar, LogLevel.Debug);
            }
            else
            {
                AdvancedDebugSystem.LogError("🧪 [LOBBYROOM] ❌ Cannot subscribe to events - LobbyState is null", LogCategory.Avatar);
            }

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

                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Subscribed to LobbyController events", LogCategory.Avatar, LogLevel.Debug);
            }

            UpdateRoomInfo();
    
            StartCoroutine(DelayedRefresh());

            UpdateHostControls();
    
            // IMPORTANTE: Actualizar el display del mapa al mostrar el panel
            UpdateMapDisplay();

            AnimateRoomEntry();

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Panel fully initialized", LogCategory.Avatar, LogLevel.Debug);
        }


        public override void OnPanelHidden()
        {
            base.OnPanelHidden();

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Panel hidden, cleaning up events...", LogCategory.Avatar, LogLevel.Debug);

            if (_lobbyState != null)
            {
                _lobbyState.OnPlayerJoined.RemoveListener(OnPlayerJoined);
                _lobbyState.OnPlayerLeft.RemoveListener(OnPlayerLeft);
                _lobbyState.OnPlayerUpdated.RemoveListener(OnPlayerUpdated);
                _lobbyState.OnPlayerCountChanged.RemoveListener(OnPlayerCountChanged);
                _lobbyState.OnAllPlayersReady.RemoveListener(OnAllPlayersReadyChanged);
                _lobbyState.OnMapChanged.RemoveListener(OnMapChangedByHost);

                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Unsubscribed from LobbyState events", LogCategory.Avatar, LogLevel.Debug);
            }

            if (_lobbyController != null)
            {
                _lobbyController.OnGameStarting.RemoveAllListeners();
                _lobbyController.OnGameStartFailed.RemoveAllListeners();
                _lobbyController.OnActionFailed.RemoveAllListeners();

                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Unsubscribed from LobbyController events", LogCategory.Avatar, LogLevel.Debug);
            }
        }

        private System.Collections.IEnumerator DelayedRefresh()
        {
            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Waiting before refresh...", LogCategory.Avatar, LogLevel.Debug);
    
            // Esperar 2 frames para asegurar que todo esté inicializado
            yield return null;
            yield return null;
    
            // Ahora sí refrescar
            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Executing delayed refresh...", LogCategory.Avatar, LogLevel.Debug);
            RefreshPlayersList();
    
            // Si aún no hay jugadores, intentar de nuevo
            if (_lobbyState != null && _lobbyState.PlayerCount == 0)
            {
                AdvancedDebugSystem.LogWarning("🧪 [LOBBYROOM] No players found, retrying in 0.5s...", LogCategory.Avatar);
                yield return new WaitForSeconds(0.5f);
                RefreshPlayersList();
            }
        }

        #region  Map Selection

        private async void StartGameWithSelectedMap()
        {
            if (_lobbyController == null || _networkBootstrapper == null)
            {
                AdvancedDebugSystem.LogError("🧪 [LOBBYROOM] Controller or Bootstrapper not available", LogCategory.Avatar);
                return;
            }
    
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] === STARTING GAME SEQUENCE ===", LogCategory.Avatar, LogLevel.Debug);
            
            // Paso 1: Sincronizar el mapa con todos los clientes
            await SyncMapWithAllClients();
            
            // Paso 2: Verificar sincronización
            if (!await VerifyMapSynchronization())
            {
                AdvancedDebugSystem.LogError("[LOBBYROOM] Map synchronization failed, aborting game start", LogCategory.Avatar);
                ShowStatusMessage("Failed to sync map with all players", MessageType.Error);
                return;
            }
    
            // Paso 3: Iniciar el juego
            AdvancedDebugSystem.Log($"[LOBBYROOM] ✅ All checks passed, starting game...", LogCategory.Avatar, LogLevel.Debug);
            _lobbyController.StartGame();
        }
        
        private async System.Threading.Tasks.Task SyncMapWithAllClients()
        {
            // Asegurar que el mapa correcto esté configurado
            string finalMap = "";
    
            // Obtener el mapa del host
            if (_lobbyState != null)
            {
                var hostPlayer = _lobbyState.HostPlayer;
                if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.SelectedMap.ToString()))
                {
                    finalMap = hostPlayer.SelectedMap.ToString();
                }
            }
    
            // Fallback al mapa local
            if (string.IsNullOrEmpty(finalMap))
            {
                finalMap = !string.IsNullOrEmpty(_selectedMapName) 
                    ? _selectedMapName 
                    : _networkBootstrapper.SelectedSceneName;
            }
    
            AdvancedDebugSystem.Log($"[LOBBYROOM] 🗺️ Final map determined: {finalMap}", LogCategory.Avatar, LogLevel.Debug);
            
            // Actualizar NetworkBootstrapper local (host)
            _networkBootstrapper.SelectedSceneName = finalMap;
            
            // Solo enviar RPC si somos el host
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost == true && _lobbyState != null)
            {
                var hostPlayer = _lobbyState.LocalPlayer;
                if (hostPlayer != null && hostPlayer.IsHost)
                {
                    AdvancedDebugSystem.Log($"[LOBBYROOM] 📡 Sending map sync RPC to all clients: {finalMap}", LogCategory.Avatar, LogLevel.Debug);
                    hostPlayer.RPC_ChangeMap(finalMap);
                    
                    // Esperar más tiempo para asegurar propagación completa
                    await System.Threading.Tasks.Task.Delay(750);
                    
                    // Verificar que nuestro propio NetworkBootstrapper esté actualizado
                    if (_networkBootstrapper.SelectedSceneName != finalMap)
                    {
                        AdvancedDebugSystem.LogWarning($"[LOBBYROOM] NetworkBootstrapper not updated, forcing: {finalMap}", LogCategory.Avatar);
                        _networkBootstrapper.SelectedSceneName = finalMap;
                    }
                }
            }
            else
            {
                AdvancedDebugSystem.Log($"[LOBBYROOM] Not host, skipping RPC send. IsHost: {lobbyInfo?.IsHost}", LogCategory.Avatar, LogLevel.Debug);
            }
        }
        
        private async System.Threading.Tasks.Task<bool> VerifyMapSynchronization()
        {
            if (_lobbyState == null || _networkBootstrapper == null) return false;
            
            string expectedMap = _networkBootstrapper.SelectedSceneName;
            AdvancedDebugSystem.Log($"[LOBBYROOM] 🔍 Verifying map synchronization: {expectedMap}", LogCategory.Avatar, LogLevel.Debug);
            
            // En Photon Fusion, solo el host puede actualizar SelectedMap
            // Verificamos que:
            // 1. El host tenga el mapa correcto
            // 2. Todos los NetworkBootstrapper estén sincronizados
            
            int maxAttempts = 5;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                bool hostHasCorrectMap = false;
                var players = _lobbyState.GetPlayersList();
                
                // Verificar que el host tenga el mapa correcto
                foreach (var player in players)
                {
                    if (player == null) continue;
                    
                    if (player.IsHost)
                    {
                        string hostMap = player.SelectedMap.ToString();
                        AdvancedDebugSystem.Log($"[LOBBYROOM] Host player {player.GetDisplayName()} has map: '{hostMap}'", LogCategory.Avatar, LogLevel.Debug);
                        
                        if (!string.IsNullOrEmpty(hostMap) && hostMap == expectedMap)
                        {
                            hostHasCorrectMap = true;
                            AdvancedDebugSystem.Log($"[LOBBYROOM] ✅ Host has correct map: {expectedMap}", LogCategory.Avatar, LogLevel.Debug);
                        }
                        else
                        {
                            AdvancedDebugSystem.LogWarning($"[LOBBYROOM] Host has incorrect map: '{hostMap}', expected '{expectedMap}'", LogCategory.Avatar);
                        }
                        break;
                    }
                }
                
                // Si el host tiene el mapa correcto, asumimos que la sincronización es correcta
                // ya que RPC_NotifyMapChange actualiza el NetworkBootstrapper de todos
                if (hostHasCorrectMap)
                {
                    AdvancedDebugSystem.Log($"[LOBBYROOM] ✅ Map synchronization verified: {expectedMap}", LogCategory.Avatar, LogLevel.Debug);
                    return true;
                }
                
                AdvancedDebugSystem.Log($"[LOBBYROOM] ⏳ Attempt {attempt + 1}/{maxAttempts} - waiting for host sync...", LogCategory.Avatar, LogLevel.Debug);
                await System.Threading.Tasks.Task.Delay(200);
            }
            
            AdvancedDebugSystem.LogError($"[LOBBYROOM] ❌ Failed to sync host with map after {maxAttempts} attempts", LogCategory.Avatar);
            
            // Log adicional para debug
            var allPlayers = _lobbyState.GetPlayersList();
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    AdvancedDebugSystem.LogError($"[LOBBYROOM] Player {player.GetDisplayName()}: IsHost={player.IsHost}, SelectedMap='{player.SelectedMap}'", LogCategory.Avatar);
                }
            }
            
            return false;
        }
        
        private void PopulateMapSelection()
        {
            if (_networkBootstrapper == null || mapButtonsContainer == null || mapButtonPrefab == null)
                return;
    
            foreach (var btn in _mapButtons)
            {
                if (btn != null) Destroy(btn);
            }
            _mapButtons.Clear();

            var availableScenes = _networkBootstrapper.GetAvailableScenes();

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Populating {availableScenes.Count} maps", LogCategory.Avatar, LogLevel.Debug);

            for (int i = 0; i < availableScenes.Count; i++)
            {
                var sceneInfo = availableScenes[i];
                GameObject mapBtn = Instantiate(mapButtonPrefab, mapButtonsContainer);
    
                var nameText = mapBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = sceneInfo.displayName;
                }
    
                var previewImage = mapBtn.transform.Find("Map_View");
                if (previewImage != null && sceneInfo.previewImage != null)
                {
                    previewImage.GetComponent<Image>().sprite = sceneInfo.previewImage;
                }
    
        
                var button = mapBtn.GetComponent<InteractableButton3D>();
                if (button != null)
                {
                    string sceneName = sceneInfo.sceneName;
                    button.OnButtonPressed.AddListener(() => SelectMap(sceneName));
                }
    
                _mapButtons.Add(mapBtn);
    
                mapBtn.transform.localScale = Vector3.zero;
                mapBtn.transform.DOScale(Vector3.one, 0.3f).SetDelay(i * 0.1f).SetEase(Ease.OutBack);
            }
        }
        
        private void SelectMap(string mapName)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] === SELECT MAP START ===", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Map to select: {mapName}", LogCategory.Avatar, LogLevel.Debug);

            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Is Host: {lobbyInfo?.IsHost}", LogCategory.Avatar, LogLevel.Debug);

            if (lobbyInfo?.IsHost != true)
            {
                ShowStatusMessage("Only host can change map", MessageType.Warning);
                AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Not host, cannot change map", LogCategory.Avatar, LogLevel.Debug);
                return;
            }

            if (!_networkBootstrapper.IsValidScene(mapName))
            {
                ShowStatusMessage("Invalid map selection", MessageType.Error);
                AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] Invalid scene: {mapName}", LogCategory.Avatar);
                return;
            }

            _selectedMapName = mapName;
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Local map name set to: {_selectedMapName}", LogCategory.Avatar, LogLevel.Debug);
    
            // Actualizar NetworkBootstrapper inmediatamente
            _networkBootstrapper.SelectedSceneName = mapName;

            var localPlayer = _lobbyState?.LocalPlayer;
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Local player exists: {localPlayer != null}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Local player is host: {localPlayer?.IsHost}", LogCategory.Avatar, LogLevel.Debug);

            if (localPlayer != null && localPlayer.IsHost)
            {
                AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 📡 Calling RPC_ChangeMap with: {mapName}", LogCategory.Avatar, LogLevel.Debug);
                localPlayer.RPC_ChangeMap(mapName);
            }
            else
            {
                AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] ❌ Cannot call RPC - player not host or null", LogCategory.Avatar);
            }

            UpdateMapDisplay();

            if (mapSelectionPanel != null)
            {
                mapSelectionPanel.transform.DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => mapSelectionPanel.SetActive(false));
            }

            ShowStatusMessage($"Map changed to: {mapName}", MessageType.Info);
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] === SELECT MAP END ===", LogCategory.Avatar, LogLevel.Debug);
        }

        
        private void OnMapChangedByHost(string newMapName)
        {
            AdvancedDebugSystem.Log($"[LOBBYROOM] 🗺️ Map changed by host to: {newMapName}", LogCategory.Avatar, LogLevel.Debug);
    
            if (string.IsNullOrEmpty(newMapName)) 
            {
                AdvancedDebugSystem.LogWarning("[LOBBYROOM] Received empty map name from host", LogCategory.Avatar);
                return;
            }

            _selectedMapName = newMapName;
            AdvancedDebugSystem.Log($"[LOBBYROOM] Local _selectedMapName updated to: {_selectedMapName}", LogCategory.Avatar, LogLevel.Debug);
    
            // Actualizar NetworkBootstrapper (CRITICO para clientes)
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.SelectedSceneName = newMapName;
                AdvancedDebugSystem.Log($"[LOBBYROOM] ✅ NetworkBootstrapper.SelectedSceneName updated to: {newMapName}", LogCategory.Avatar, LogLevel.Debug);
            }
            else
            {
                AdvancedDebugSystem.LogError("[LOBBYROOM] NetworkBootstrapper is null! Cannot update scene name.", LogCategory.Avatar);
            }
    
            UpdateMapDisplay();

            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost == false)
            {
                ShowStatusMessage($"Host changed map to: {newMapName}", MessageType.Info);
                AdvancedDebugSystem.Log($"[LOBBYROOM] 📢 Showed map change message to client", LogCategory.Avatar, LogLevel.Debug);
            }
        }


        #endregion

        #region Event Handlers

        private void OnPlayerJoined(LobbyPlayer player)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 🎉 Player joined: {player.GetDisplayName()}", LogCategory.Avatar, LogLevel.Debug);

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} joined the lobby", MessageType.Info);

            PlayJoinEffect();
        }

        private void OnPlayerLeft(LobbyPlayer player)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 👋 Player left: {player.GetDisplayName()}", LogCategory.Avatar, LogLevel.Debug);

            RefreshPlayersList();
            ShowStatusMessage($"{player.PlayerName} left the lobby", MessageType.Warning);
        }

        private void OnPlayerUpdated(LobbyPlayer player)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 🔄 Player updated: {player.GetDisplayName()} - Ready: {player.IsReady}", LogCategory.Avatar, LogLevel.Debug);

            var playerItem = _playerItems.FirstOrDefault(item =>
                item.gameObject.activeSelf && item.GetPlayerRef() == player.PlayerRef);

            if (playerItem != null)
            {
                playerItem.UpdatePlayerData(player);
            }

            if (player.IsLocalPlayer)
            {
                UpdateLocalPlayerControls(player);
            }

            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void OnPlayerCountChanged(int current, int max)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 📊 Player count changed: {current}/{max}", LogCategory.Avatar, LogLevel.Debug);

            UpdatePlayerCount();
            UpdateStartButton();
        }

        private void OnAllPlayersReadyChanged(bool allReady)
        {
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] 🎯 All players ready changed: {allReady}", LogCategory.Avatar, LogLevel.Debug);

            UpdateStartButton();

            if (allReady && _lobbyState != null && _lobbyState.PlayerCount > 1)
            {
                ShowStatusMessage("All players ready! Host can start the game.", MessageType.Success);

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
            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Updating room info...", LogCategory.Avatar, LogLevel.Debug);

            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo == null)
            {
                AdvancedDebugSystem.LogWarning("🧪 [LOBBYROOM] ⚠️ Cannot update room info - LobbyController returned null", LogCategory.Avatar);
                return;
            }

            if (roomNameText != null)
            {
                roomNameText.text = lobbyInfo.RoomName ?? "Room";
            }

            if (roomCodeText != null)
            {
                roomCodeText.text = $"#{lobbyInfo.RoomCode}";
            }

            if (roomStatusIndicator != null)
            {
                roomStatusIndicator.color = lobbyInfo.IsInLobby ? readyColor : notReadyColor;
            }

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] ✅ Room info updated - {lobbyInfo.RoomName}", LogCategory.Avatar, LogLevel.Debug);
        }

        private void RefreshPlayersList()
        {
            if (_lobbyState == null)
            {
                AdvancedDebugSystem.LogWarning("🧪 [LOBBYROOM] ⚠️ Cannot refresh players list - LobbyState is null", LogCategory.Avatar);
                return;
            }

            AdvancedDebugSystem.Log("🧪 [LOBBYROOM] Refreshing players list...", LogCategory.Avatar, LogLevel.Debug);

            try
            {
                foreach (var item in _playerItems)
                {
                    if (item != null && item.gameObject != null)
                        item.ResetItem();
                        item.gameObject.SetActive(false);
                }

                List<LobbyPlayer> players = null;
                try
                {
                    players = _lobbyState.GetPlayersList(hostFirst: true);
                }
                catch (System.Exception e)
                {
                    AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] ❌ Error getting players list: {e.Message}", LogCategory.Avatar);
                    return;
                }

                AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Found {players?.Count ?? 0} players to display", LogCategory.Avatar, LogLevel.Debug);

                if (players == null || players.Count == 0)
                {
                    AdvancedDebugSystem.LogWarning("🧪 [LOBBYROOM] ⚠️ No players found in LobbyState!", LogCategory.Avatar);
                    return;
                }

                for (int i = 0; i < players.Count && i < _playerItems.Count; i++)
                {
                    LobbyPlayerItem item = _playerItems[i];
                    LobbyPlayer player = players[i];

                    if (player == null)
                    {
                        AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] ❌ Player at index {i} is null!", LogCategory.Avatar);
                        continue;
                    }

                    AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Displaying player {i}:", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Name: {player.PlayerName.ToString()}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Display Name: {player.GetDisplayName()}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Is Host: {player.IsHost}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Is Local: {player.IsLocalPlayer}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Is Ready: {player.IsReady}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"  - Player Ref: {player.PlayerRef}", LogCategory.Avatar, LogLevel.Debug);

                    item.UpdatePlayerData(player);
                    item.gameObject.SetActive(true);

                    //float targetY = -i * 0.15f; // Espaciado entre items
                    //item.transform.localPosition = new Vector3(0, targetY + 0.3f, 0);

                    /*try
                    {
                        item.transform.DOLocalMoveY(targetY, 0.4f)
                            .SetDelay(i * 0.1f)
                            .SetEase(Ease.OutBack);
                    }
                    catch (System.Exception e)
                    {
                        AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] Animation error: {e.Message}", LogCategory.Avatar);
                        item.transform.localPosition = new Vector3(0, targetY, 0);
                    }*/
                }

                UpdatePlayerCount();
                AdvancedDebugSystem.Log("🧪 [LOBBYROOM] ✅ Players list refreshed successfully", LogCategory.Avatar, LogLevel.Debug);
            }
            catch (System.Exception e)
            {
                AdvancedDebugSystem.LogError($"🧪 [LOBBYROOM] ❌ Critical error in RefreshPlayersList: {e.Message}\n{e.StackTrace}", LogCategory.Avatar);

                ShowErrorInPlayerList("Error loading players");
            }
        }
        
        private void ShowErrorInPlayerList(string errorMessage)
        {
            if (_playerItems.Count > 0 && _playerItems[0] != null)
            {
                var firstItem = _playerItems[0];
                firstItem.gameObject.SetActive(true);
        
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

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Updating local player controls - Ready: {_isLocalPlayerReady}", LogCategory.Avatar, LogLevel.Debug);

            if (readyButton != null && readyButtonText != null)
            {
                readyButtonText.text = _isLocalPlayerReady ? "Not Ready" : "Ready";
            }

            if (readyStatusIndicator != null)
            {
                readyStatusIndicator.color = _isLocalPlayerReady ? readyColor : notReadyColor;

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

            // Prioridad: 1) Mapa del host en LobbyState, 2) Mapa local seleccionado, 3) Mapa por defecto
            string currentMap = "";
    
            // Primero intentar obtener el mapa del host
            if (_lobbyState != null)
            {
                var hostPlayer = _lobbyState.HostPlayer;
                if (hostPlayer != null && !string.IsNullOrEmpty(hostPlayer.SelectedMap.ToString()))
                {
                    currentMap = hostPlayer.SelectedMap.ToString();
                    AdvancedDebugSystem.Log($"[LOBBYROOM] Using host's selected map: {currentMap}", LogCategory.Avatar, LogLevel.Debug);
                }
            }
    
            // Si no hay mapa del host, usar el local o el por defecto
            if (string.IsNullOrEmpty(currentMap))
            {
                currentMap = !string.IsNullOrEmpty(_selectedMapName) 
                    ? _selectedMapName 
                    : _networkBootstrapper.SelectedSceneName;
                AdvancedDebugSystem.Log($"[LOBBYROOM] Using local/default map: {currentMap}", LogCategory.Avatar, LogLevel.Debug);
            }
    
            // Actualizar el mapa local
            _selectedMapName = currentMap;
    
            // Actualizar NetworkBootstrapper
            _networkBootstrapper.SelectedSceneName = currentMap;

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
            
                AdvancedDebugSystem.LogWarning($"[LOBBYROOM] Scene info not found for: {currentMap}", LogCategory.Avatar);
            }
        }


        private void UpdateHostControls()
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            bool isHost = lobbyInfo?.IsHost ?? false;

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Updating host controls - Is Host: {isHost}", LogCategory.Avatar, LogLevel.Debug);

            if (hostControlsPanel != null)
            {
                hostControlsPanel.SetActive(isHost);
            }
    
            if (changeMapButton != null)
            {
                changeMapButton.gameObject.SetActive(isHost);
            }

            if (isHost && lobbyInfo != null)
            {
                UpdateMapDisplay();
            }

            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            if (startGameButton == null || _lobbyController == null) return;

            bool canStart = _lobbyController.CanStartGame;
            startGameButton.SetInteractable(canStart);

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Start button - Can start: {canStart}", LogCategory.Avatar, LogLevel.Debug);

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
        
        private void ToggleMapSelection()
        {
            if (mapSelectionPanel == null) return;

            bool isActive = mapSelectionPanel.activeSelf;
            mapSelectionPanel.SetActive(!isActive);

            if (!isActive)
            {
                PopulateMapSelection();
    
                mapSelectionPanel.transform.localScale = Vector3.zero;
                mapSelectionPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }

        
        private void ToggleRoomSettings()
        {
            if (roomSettingsPanel != null)
            {
                bool isActive = roomSettingsPanel.activeSelf;
                roomSettingsPanel.SetActive(!isActive);

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

            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Player item clicked: {player.GetDisplayName()}", LogCategory.Avatar, LogLevel.Debug);

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
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Max players changed to: {maxPlayers}", LogCategory.Avatar, LogLevel.Debug);
        }

        private void OnRoomOpenChanged(bool isOpen)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar RPC para cambiar visibilidad de sala
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Room open changed to: {isOpen}", LogCategory.Avatar, LogLevel.Debug);
        }

        private void OnPrivateRoomChanged(bool isPrivate)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar RPC para cambiar privacidad
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Private room changed to: {isPrivate}", LogCategory.Avatar, LogLevel.Debug);
        }

        private void OnDifficultyChanged(float value)
        {
            var lobbyInfo = _lobbyController?.GetLobbyInfo();
            if (lobbyInfo?.IsHost != true) return;

            // TODO: Implementar configuración de dificultad
            AdvancedDebugSystem.Log($"🧪 [LOBBYROOM] Difficulty changed to: {value}", LogCategory.Avatar, LogLevel.Debug);
        }
        
        

        #endregion

        #region Visual Effects & Feedback

        private void AnimateRoomEntry()
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

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

            Color messageColor = type switch
            {
                MessageType.Success => readyColor,
                MessageType.Warning => Color.yellow,
                MessageType.Error => notReadyColor,
                _ => Color.white
            };

            statusText.text = message;
            statusText.color = messageColor;

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

            if (backButton != null)
            {
                backButton.OnButtonPressed.RemoveAllListeners();
                backButton.OnButtonPressed.AddListener(async () =>
                {
                    ShowStatusMessage("Leaving room...", MessageType.Info);
                    _lobbyController?.LeaveLobby();

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
            AdvancedDebugSystem.Log("=== LobbyRoom Debug Status ===", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"LobbyState: {_lobbyState != null}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"LobbyController: {_lobbyController != null}", LogCategory.Avatar, LogLevel.Debug);

            if (_lobbyState != null)
            {
                var stats = _lobbyState.GetLobbyStats();
                AdvancedDebugSystem.Log($"Players: {stats.TotalPlayers}/{stats.MaxPlayers}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"Ready: {stats.ReadyPlayers}/{stats.TotalPlayers}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"All Ready: {stats.AllReady}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"Host: {stats.HostName}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"Local: {stats.LocalPlayerName}", LogCategory.Avatar, LogLevel.Debug);
            }

            if (_lobbyController != null)
            {
                var info = _lobbyController.GetLobbyInfo();
                if (info != null)
                {
                    AdvancedDebugSystem.Log($"Room: {info.RoomName}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"Is Host: {info.IsHost}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"Can Start: {info.CanStart}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"Status: {info.StatusText}", LogCategory.Avatar, LogLevel.Debug);
                }
            }

            AdvancedDebugSystem.Log("================================", LogCategory.Avatar, LogLevel.Debug);
        }

        [ContextMenu("Debug: Force Refresh")]
        private void DebugForceRefresh()
        {
            AdvancedDebugSystem.Log("🧪 [DEBUG] Forcing refresh...", LogCategory.Avatar, LogLevel.Debug);
            RefreshPlayersList();
            UpdateRoomInfo();
            UpdateHostControls();
        }

        [ContextMenu("Debug: Force Refresh Players")]
        private void DebugForceRefreshPlayers()
        {
            AdvancedDebugSystem.Log("🧪 [DEBUG] === FORCE REFRESH PLAYERS ===", LogCategory.Avatar, LogLevel.Debug);
    
            if (_lobbyState == null)
            {
                AdvancedDebugSystem.LogError("🧪 [DEBUG] LobbyState is null!", LogCategory.Avatar);
                _lobbyState = LobbyState.Instance;
                if (_lobbyState == null)
                {
                    AdvancedDebugSystem.LogError("🧪 [DEBUG] LobbyState.Instance is also null!", LogCategory.Avatar);
                    return;
                }
            }
    
            var players = _lobbyState.GetPlayersList(hostFirst: true);
            AdvancedDebugSystem.Log($"🧪 [DEBUG] Players in LobbyState: {players.Count}", LogCategory.Avatar, LogLevel.Debug);
    
            foreach (var player in players)
            {
                if (player != null)
                {
                    AdvancedDebugSystem.Log($"🧪 [DEBUG] - {player.GetDisplayName()} (Ref: {player.PlayerRef})", LogCategory.Avatar, LogLevel.Debug);
                }
                else
                {
                    AdvancedDebugSystem.LogError("🧪 [DEBUG] - NULL PLAYER!", LogCategory.Avatar);
                }
            }
    
            RefreshPlayersList();
    
            AdvancedDebugSystem.Log("🧪 [DEBUG] === END FORCE REFRESH ===", LogCategory.Avatar, LogLevel.Debug);
        }
        
        [ContextMenu("Debug: Complete System State")]
        private void DebugCompleteSystemState()
        {
            AdvancedDebugSystem.Log("=== COMPLETE SYSTEM DEBUG STATE ===", LogCategory.Avatar, LogLevel.Debug);
            
            // LobbyRoom state
            AdvancedDebugSystem.Log($"[LobbyRoom] Selected Map Name: {_selectedMapName}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"[LobbyRoom] NetworkBootstrapper exists: {_networkBootstrapper != null}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"[LobbyRoom] LobbyController exists: {_lobbyController != null}", LogCategory.Avatar, LogLevel.Debug);
            AdvancedDebugSystem.Log($"[LobbyRoom] LobbyState exists: {_lobbyState != null}", LogCategory.Avatar, LogLevel.Debug);
            
            // NetworkBootstrapper state
            if (_networkBootstrapper != null)
            {
                AdvancedDebugSystem.Log($"[NetworkBootstrapper] Selected Scene: {_networkBootstrapper.SelectedSceneName}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"[NetworkBootstrapper] Is Host: {_networkBootstrapper.IsHost}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"[NetworkBootstrapper] Is In Room: {_networkBootstrapper.IsInRoom}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"[NetworkBootstrapper] Is Connected: {_networkBootstrapper.IsConnected}", LogCategory.Avatar, LogLevel.Debug);
            }
            
            // LobbyState players and maps
            if (_lobbyState != null)
            {
                var players = _lobbyState.GetPlayersList();
                AdvancedDebugSystem.Log($"[LobbyState] Player count: {players.Count}", LogCategory.Avatar, LogLevel.Debug);
                
                foreach (var player in players)
                {
                    if (player != null)
                    {
                        AdvancedDebugSystem.Log($"[LobbyState] Player: {player.GetDisplayName()}", LogCategory.Avatar, LogLevel.Debug);
                        AdvancedDebugSystem.Log($"  - IsHost: {player.IsHost}", LogCategory.Avatar, LogLevel.Debug);
                        AdvancedDebugSystem.Log($"  - IsReady: {player.IsReady}", LogCategory.Avatar, LogLevel.Debug);
                        AdvancedDebugSystem.Log($"  - SelectedMap: '{player.SelectedMap}'", LogCategory.Avatar, LogLevel.Debug);
                        AdvancedDebugSystem.Log($"  - IsLocalPlayer: {player.IsLocalPlayer}", LogCategory.Avatar, LogLevel.Debug);
                    }
                }
                
                AdvancedDebugSystem.Log($"[LobbyState] All Players Ready: {_lobbyState.AllPlayersReady}", LogCategory.Avatar, LogLevel.Debug);
                AdvancedDebugSystem.Log($"[LobbyState] Selected Map: {_lobbyState.GetSelectedMap()}", LogCategory.Avatar, LogLevel.Debug);
            }
            
            // LobbyController state
            if (_lobbyController != null)
            {
                AdvancedDebugSystem.Log($"[LobbyController] Can Start Game: {_lobbyController.CanStartGame}", LogCategory.Avatar, LogLevel.Debug);
                var info = _lobbyController.GetLobbyInfo();
                if (info != null)
                {
                    AdvancedDebugSystem.Log($"[LobbyController] Is Host: {info.IsHost}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"[LobbyController] Current Players: {info.CurrentPlayers}", LogCategory.Avatar, LogLevel.Debug);
                    AdvancedDebugSystem.Log($"[LobbyController] Ready Players: {info.ReadyPlayers}", LogCategory.Avatar, LogLevel.Debug);
                }
            }
            
            AdvancedDebugSystem.Log("==================================", LogCategory.Avatar, LogLevel.Debug);
        }
        
        [ContextMenu("Debug: Test Map Synchronization")]
        private async void DebugTestMapSynchronization()
        {
            AdvancedDebugSystem.Log("=== TESTING MAP SYNCHRONIZATION ===", LogCategory.Avatar, LogLevel.Debug);
            
            // Estado antes
            AdvancedDebugSystem.Log("--- BEFORE SYNC ---", LogCategory.Avatar, LogLevel.Debug);
            DebugCompleteSystemState();
            
            // Simular sincronización
            AdvancedDebugSystem.Log("--- STARTING SYNC ---", LogCategory.Avatar, LogLevel.Debug);
            await SyncMapWithAllClients();
            
            // Estado después
            AdvancedDebugSystem.Log("--- AFTER SYNC ---", LogCategory.Avatar, LogLevel.Debug);
            DebugCompleteSystemState();
            
            // Verificar sincronización
            AdvancedDebugSystem.Log("--- VERIFYING SYNC ---", LogCategory.Avatar, LogLevel.Debug);
            bool syncResult = await VerifyMapSynchronization();
            AdvancedDebugSystem.Log($"Synchronization result: {syncResult}", LogCategory.Avatar, LogLevel.Debug);
            
            AdvancedDebugSystem.Log("=== TEST COMPLETED ===", LogCategory.Avatar, LogLevel.Debug);
        }

        #endregion

        private void OnDestroy()
        {
            _statusTextTween?.Kill();
            _readyButtonTween?.Kill();
        }
    }
}
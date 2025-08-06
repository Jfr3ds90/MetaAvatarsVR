using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using DG.Tweening;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using HackMonkeys.UI.Theme;
using System.Linq;
using System.Collections;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Panel VR para navegar y unirse a salas disponibles
    /// Versión refactorizada con estado persistente y actualización incremental
    /// </summary>
    public class LobbyBrowser : MenuPanel
    {
        #region Browser State Management
        /// <summary>
        /// Estado persistente del browser para mantener selección y cache
        /// </summary>
        private class BrowserState
        {
            public string SelectedSessionName { get; set; }
            public SessionInfo SelectedSession { get; set; }
            public Dictionary<string, SessionInfo> CachedSessions { get; set; } = new();
            public float LastRefreshTime { get; set; }
            public bool IsRefreshing { get; set; }
            public bool HasInitialLoad { get; set; }
            public bool IsJoiningRoom { get; set; }
            public int ConsecutiveRefreshFailures { get; set; }
        }
        #endregion

        [Header("UI Components")]
        [SerializeField] private Transform roomListContainer;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private InteractableButton3D refreshButton;
        [SerializeField] private InteractableButton3D createRoomButton;
        
        [Header("Room Info Display")]
        [SerializeField] private GameObject selectedRoomInfo;
        [SerializeField] private TextMeshProUGUI selectedRoomName;
        [SerializeField] private TextMeshProUGUI selectedRoomPlayers;
        [SerializeField] private TextMeshProUGUI selectedRoomStatus;
        [SerializeField] private InteractableButton3D joinButton;
        [SerializeField] private Image selectedRoomBackground;
        
        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject noRoomsMessage;
        
        [Header("Filters")]
        [SerializeField] private Toggle showFullRoomsToggle;
        [SerializeField] private Toggle showInProgressToggle;
        [SerializeField] private TMP_Dropdown sortDropdown;
        
        [Header("Visual Settings")]
        [SerializeField] private float roomItemSpacing = 0.15f;
        [SerializeField] private int maxVisibleRooms = 6;
        [SerializeField] private ScrollRect scrollView;
        
        [Header("Refresh Settings")]
        [SerializeField] private float minRefreshInterval = 3f;
        [SerializeField] private float autoRefreshInterval = 10f;
        [SerializeField] private int maxConsecutiveFailures = 3;
        
        [Header("Color Theme")]
        [SerializeField] private UIColorTheme colorTheme;
        
        private NetworkBootstrapper _networkBootstrapper;
        private List<RoomItem> _roomItems = new List<RoomItem>();
        private List<SessionInfo> _currentSessions = new List<SessionInfo>();
        
        // Estado persistente
        private BrowserState _browserState = new BrowserState();
        private Coroutine _autoRefreshCoroutine;
        
        private bool _showFullRooms = true;
        private bool _showInProgressRooms = true;
        private SortType _currentSortType = SortType.PlayerCount;
        private GameCore _gameCore;
        
        private enum SortType
        {
            Name,
            PlayerCount,
            Ping
        }

        private void Start()
        {
            _gameCore = GameCore.Instance;
            
            if (colorTheme == null)
            {
                colorTheme = UIColorTheme.Instance;
            }
            
            ApplyColorTheme();
        }

        protected override void SetupPanel()
        {
            base.SetupPanel();
            
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            ConfigureButtons();
            ConfigureFilters();
            
            if (selectedRoomInfo != null)
            {
                selectedRoomInfo.SetActive(false);
                
                if (selectedRoomBackground != null)
                {
                    selectedRoomBackground.color = colorTheme.GetBackgroundColor();
                }
            }
                
            InitializeRoomItemPool();
        }
        
        private void ApplyColorTheme()
        {
            if (statusText != null)
                statusText.color = colorTheme.TextWhite;
                
            if (selectedRoomName != null)
                selectedRoomName.color = colorTheme.TextWhite;
                
            if (selectedRoomPlayers != null)
                selectedRoomPlayers.color = colorTheme.TextWhite;
                
            if (selectedRoomStatus != null)
                selectedRoomStatus.color = colorTheme.TextWhite;
                
            if (noRoomsMessage != null)
            {
                var messageText = noRoomsMessage.GetComponentInChildren<TextMeshProUGUI>();
                if (messageText != null)
                    messageText.color = colorTheme.SecondaryGray;
            }
        }
        
        private void ConfigureButtons()
        {
            if (refreshButton != null)
            {
                refreshButton.OnButtonPressed.AddListener(() => RefreshRoomList(true));
            }
            
            if (createRoomButton != null)
            {
                createRoomButton.OnButtonPressed.AddListener(() => _uiManager.ShowPanel(PanelID.CreateLobby));
            }
            
            if (joinButton != null)
            {
                joinButton.OnButtonPressed.AddListener(JoinSelectedRoom);
                joinButton.SetInteractable(false);
            }
        }
        
        private void ConfigureFilters()
        {
            if (showFullRoomsToggle != null)
            {
                showFullRoomsToggle.isOn = _showFullRooms;
                showFullRoomsToggle.onValueChanged.AddListener((value) =>
                {
                    _showFullRooms = value;
                    ApplyFilters();
                });
            }
            
            if (showInProgressToggle != null)
            {
                showInProgressToggle.isOn = _showInProgressRooms;
                showInProgressToggle.onValueChanged.AddListener((value) =>
                {
                    _showInProgressRooms = value;
                    ApplyFilters();
                });
            }
            
            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(new List<string> { "Player Count", "Name", "Ping" });
                sortDropdown.value = (int)_currentSortType;
                sortDropdown.onValueChanged.AddListener((value) =>
                {
                    _currentSortType = (SortType)value;
                    ApplyFilters();
                });
            }
        }
        
        private void InitializeRoomItemPool()
        {
            for (int i = 0; i < maxVisibleRooms; i++)
            {
                GameObject roomObj = Instantiate(roomItemPrefab, roomListContainer);
                RoomItem roomItem = roomObj.GetComponent<RoomItem>();
                
                if (roomItem != null)
                {
                    roomItem.Initialize(i, OnRoomSelected, colorTheme);
                    roomItem.gameObject.SetActive(false);
                    _roomItems.Add(roomItem);
                }
            }
        }
        
        public override void OnPanelShown()
        {
            base.OnPanelShown();
    
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnSessionListUpdatedEvent.AddListener(OnExternalSessionUpdate);
            }
    
            // Primera carga con indicador visual
            RefreshRoomList(true);
    
            // Iniciar auto-refresh inteligente
            if (_autoRefreshCoroutine != null)
            {
                StopCoroutine(_autoRefreshCoroutine);
            }
            _autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
        }
        
        public override void OnPanelHidden()
        {
            base.OnPanelHidden();
    
            // Detener auto-refresh
            if (_autoRefreshCoroutine != null)
            {
                StopCoroutine(_autoRefreshCoroutine);
                _autoRefreshCoroutine = null;
            }
    
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnSessionListUpdatedEvent.RemoveListener(OnExternalSessionUpdate);
            }
            
            // Limpiar estado pero mantener cache
            _browserState.IsRefreshing = false;
            _browserState.IsJoiningRoom = false;
        }
        
        #region Refresh System
        
        /// <summary>
        /// Sistema de refresh inteligente con cache y validación
        /// </summary>
        private async void RefreshRoomList(bool forceRefresh = false)
        {
            // Validaciones de rate limiting
            if (!forceRefresh && Time.time - _browserState.LastRefreshTime < minRefreshInterval)
            {
                Debug.Log("[LobbyBrowser] Skipping refresh - too soon");
                return;
            }
            
            if (_browserState.IsRefreshing)
            {
                Debug.Log("[LobbyBrowser] Already refreshing...");
                return;
            }
            
            if (_browserState.IsJoiningRoom)
            {
                Debug.Log("[LobbyBrowser] Cannot refresh while joining room");
                return;
            }
    
            _browserState.IsRefreshing = true;
            _browserState.LastRefreshTime = Time.time;
    
            // Mostrar loading solo en primera carga o refresh manual
            bool showLoading = !_browserState.HasInitialLoad || forceRefresh;
            if (showLoading)
            {
                ShowLoadingState();
            }
    
            // Animar botón de refresh
            if (refreshButton != null && forceRefresh)
            {
                refreshButton.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear);
            }
    
            try
            {
                Debug.Log("[LobbyBrowser] 🔍 Requesting session list...");
                
                var sessions = await _networkBootstrapper.GetAvailableSessions();
                
                if (sessions != null && sessions.Count > 0)
                {
                    _browserState.HasInitialLoad = true;
                    _browserState.ConsecutiveRefreshFailures = 0;
                    UpdateSessionCache(sessions);
                    OnSessionListUpdated(sessions);
                    
                    Debug.Log($"[LobbyBrowser] 📋 Received {sessions.Count} sessions");
                }
                else if (!_browserState.HasInitialLoad)
                {
                    // Primera carga sin resultados
                    ShowNoRoomsState();
                }
                else
                {
                    // No hay salas pero ya teníamos data previa
                    UpdateStatusText("No rooms available", MessageType.Warning);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyBrowser] ❌ Error getting sessions: {e.Message}");
                HandleRefreshError();
            }
            finally
            {
                _browserState.IsRefreshing = false;
                
                if (showLoading && loadingIndicator != null)
                {
                    loadingIndicator.SetActive(false);
                    loadingIndicator.transform.DOKill();
                }
            }
        }
        
        /// <summary>
        /// Coroutine de auto-refresh inteligente
        /// </summary>
        private IEnumerator AutoRefreshCoroutine()
        {
            yield return new WaitForSeconds(autoRefreshInterval);
            
            while (gameObject.activeInHierarchy)
            {
                // Solo refrescar si:
                // 1. No estamos refrescando
                // 2. No estamos uniéndonos a una sala
                // 3. Ya tuvimos una carga inicial exitosa
                // 4. No hemos tenido muchos errores consecutivos
                if (!_browserState.IsRefreshing && 
                    !_browserState.IsJoiningRoom && 
                    _browserState.HasInitialLoad &&
                    _browserState.ConsecutiveRefreshFailures < maxConsecutiveFailures)
                {
                    RefreshRoomList(false); // Refresh silencioso
                }
                
                yield return new WaitForSeconds(autoRefreshInterval);
            }
        }
        
        /// <summary>
        /// Actualiza el cache de sesiones
        /// </summary>
        private void UpdateSessionCache(List<SessionInfo> newSessions)
        {
            _browserState.CachedSessions.Clear();
            foreach (var session in newSessions)
            {
                if (session != null && !string.IsNullOrEmpty(session.Name))
                {
                    _browserState.CachedSessions[session.Name] = session;
                }
            }
        }
        
        /// <summary>
        /// Maneja errores de refresh
        /// </summary>
        private void HandleRefreshError()
        {
            _browserState.ConsecutiveRefreshFailures++;
            
            if (!_browserState.HasInitialLoad)
            {
                UpdateStatusText("Error loading rooms", MessageType.Error);
                ShowNoRoomsState();
            }
            else if (_browserState.ConsecutiveRefreshFailures >= maxConsecutiveFailures)
            {
                UpdateStatusText("Connection issues detected", MessageType.Error);
                Debug.LogWarning($"[LobbyBrowser] Max consecutive failures reached ({maxConsecutiveFailures})");
            }
        }
        
        /// <summary>
        /// Callback para actualizaciones externas de sesiones
        /// </summary>
        private void OnExternalSessionUpdate(List<SessionInfo> sessions)
        {
            if (!_browserState.IsRefreshing && sessions != null)
            {
                Debug.Log($"[LobbyBrowser] External session update received: {sessions.Count} sessions");
                UpdateSessionCache(sessions);
                OnSessionListUpdated(sessions);
            }
        }
        
        #endregion
        
        #region Session List Processing
        
        private void OnSessionListUpdated(List<SessionInfo> sessions)
        {
            _currentSessions = sessions ?? new List<SessionInfo>();
            ApplyFilters();
            UpdateStatusDisplay();
        }
        
        private void ApplyFilters()
        {
            List<SessionInfo> filteredSessions = new List<SessionInfo>();
            
            foreach (var session in _currentSessions)
            {
                if (!_showFullRooms && session.PlayerCount >= session.MaxPlayers)
                    continue;
                    
                if (!_showInProgressRooms && session.IsOpen == false)
                    continue;
                    
                filteredSessions.Add(session);
            }
            
            SortSessions(filteredSessions);
            DisplayRooms(filteredSessions);
        }
        
        private void SortSessions(List<SessionInfo> sessions)
        {
            switch (_currentSortType)
            {
                case SortType.Name:
                    sessions.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                    break;
                    
                case SortType.PlayerCount:
                    sessions.Sort((a, b) => b.PlayerCount.CompareTo(a.PlayerCount));
                    break;
                    
                case SortType.Ping:
                    // TODO: Implementar cuando tengamos ping real
                    break;
            }
        }
        
        #endregion
        
        #region Room Display with State Preservation
        
        /// <summary>
        /// Muestra las salas preservando el estado de selección
        /// </summary>
        private void DisplayRooms(List<SessionInfo> sessions)
        {
            // Guardar estado de selección actual
            string previouslySelectedName = _browserState.SelectedSessionName;
            RoomItem previouslySelectedItem = null;
            
            // Actualizar items existentes sin recrear
            for (int i = 0; i < _roomItems.Count; i++)
            {
                RoomItem item = _roomItems[i];
                
                if (i < sessions.Count)
                {
                    SessionInfo session = sessions[i];
                    
                    // Actualizar datos del item (incremental si es posible)
                    if (item.gameObject.activeSelf && item.GetSessionInfo() != null)
                    {
                        // Item ya visible, actualización incremental
                        item.UpdateRoomData(session);
                    }
                    else
                    {
                        // Item nuevo o reactivado, configuración completa
                        item.SetRoomData(session);
                        
                        if (!item.gameObject.activeSelf)
                        {
                            item.gameObject.SetActive(true);
                            AnimateItemAppear(item, i);
                        }
                    }
                    
                    // Restaurar selección si coincide
                    if (!string.IsNullOrEmpty(previouslySelectedName) && session.Name == previouslySelectedName)
                    {
                        item.SetSelected(true);
                        previouslySelectedItem = item;
                        _browserState.SelectedSession = session;
                    }
                    else
                    {
                        item.SetSelected(false);
                    }
                }
                else
                {
                    // Ocultar items extras
                    if (item.gameObject.activeSelf)
                    {
                        AnimateItemDisappear(item);
                    }
                }
            }
            
            // Ajustar contenedor si es necesario
            UpdateContainerSize(sessions.Count);
            
            // Manejar caso donde la sala seleccionada desapareció
            if (!string.IsNullOrEmpty(previouslySelectedName) && previouslySelectedItem == null)
            {
                HandleSelectedRoomDisappeared();
            }
        }
        
        private void AnimateItemAppear(RoomItem item, int index)
        {
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(Vector3.one, 0.3f)
                .SetDelay(index * 0.05f)
                .SetEase(Ease.OutBack);
        }
        
        private void AnimateItemDisappear(RoomItem item)
        {
            item.transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => item.gameObject.SetActive(false));
        }
        
        private void UpdateContainerSize(int visibleCount)
        {
            if (roomListContainer != null)
            {
                float containerHeight = visibleCount * roomItemSpacing;
                var rectTransform = roomListContainer.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, containerHeight);
                }
            }
        }
        
        /// <summary>
        /// Maneja el caso cuando la sala seleccionada ya no está disponible
        /// </summary>
        private void HandleSelectedRoomDisappeared()
        {
            Debug.Log($"[LobbyBrowser] Selected room '{_browserState.SelectedSessionName}' no longer available");
            
            // Animar desaparición del panel de info
            if (selectedRoomInfo != null && selectedRoomInfo.activeSelf)
            {
                selectedRoomInfo.transform.DOScale(Vector3.one * 0.9f, 0.2f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => 
                    {
                        selectedRoomInfo.SetActive(false);
                        UpdateStatusText("Selected room is no longer available", MessageType.Warning);
                    });
            }
            
            // Limpiar estado
            _browserState.SelectedSessionName = null;
            _browserState.SelectedSession = null;
            
            if (joinButton != null)
            {
                joinButton.SetInteractable(false);
            }
        }
        
        #endregion
        
        #region Room Selection and Joining
        
        private void OnRoomSelected(SessionInfo session)
        {
            if (session == null) return;
            
            // Actualizar estado persistente
            _browserState.SelectedSession = session;
            _browserState.SelectedSessionName = session.Name;
            
            // Actualizar UI de información
            if (selectedRoomInfo != null)
            {
                if (!selectedRoomInfo.activeSelf)
                {
                    selectedRoomInfo.SetActive(true);
                    selectedRoomInfo.transform.localScale = Vector3.zero;
                    selectedRoomInfo.transform.DOScale(Vector3.one, 0.3f)
                        .SetEase(Ease.OutBack);
                }
            }
            
            if (selectedRoomName != null)
                selectedRoomName.text = session.Name;
                
            if (selectedRoomPlayers != null)
            {
                selectedRoomPlayers.text = $"{session.PlayerCount}/{session.MaxPlayers} Players";
                selectedRoomPlayers.color = colorTheme.GetPlayerCountColor(session.PlayerCount, session.MaxPlayers);
            }
                
            if (selectedRoomStatus != null)
            {
                bool canJoin = session.IsOpen && session.PlayerCount < session.MaxPlayers;
                string statusText = canJoin ? "Available" : (session.IsOpen ? "Full" : "In Progress");
                selectedRoomStatus.text = statusText;
                selectedRoomStatus.color = colorTheme.GetRoomStateColor(session.IsOpen, session.PlayerCount >= session.MaxPlayers);
            }
            
            if (joinButton != null)
            {
                bool canJoin = session.PlayerCount < session.MaxPlayers && session.IsOpen;
                joinButton.SetInteractable(canJoin);
            }
            
            // Actualizar selección visual en los items
            foreach (var item in _roomItems)
            {
                if (item.gameObject.activeSelf)
                {
                    item.SetSelected(item.GetSessionInfo()?.Name == session.Name);
                }
            }
        }
        
        private async void JoinSelectedRoom()
        {
            if (_browserState.SelectedSession == null || _networkBootstrapper == null) 
                return;
            
            if (_browserState.IsJoiningRoom)
            {
                Debug.LogWarning("[LobbyBrowser] Already joining a room");
                return;
            }
    
            Debug.Log($"[LobbyBrowser] 🎮 Attempting to join room: {_browserState.SelectedSession.Name}");
    
            _browserState.IsJoiningRoom = true;
            
            if (joinButton != null) 
                joinButton.SetInteractable(false);
        
            UpdateStatusText("Joining room...", MessageType.Info);
    
            try
            {
                bool success = await _networkBootstrapper.JoinRoom(_browserState.SelectedSession);
        
                Debug.Log($"[LobbyBrowser] Join result: {success}");
        
                if (success)
                {
                    _gameCore.OnJoinedLobby(_browserState.SelectedSession.Name, false);
                    
                    UpdateStatusText("Connected! Loading lobby...", MessageType.Success);
            
                    Debug.Log("[LobbyBrowser] ⏳ Waiting for player spawn...");
                    await System.Threading.Tasks.Task.Delay(1000);
            
                    Debug.Log("[LobbyBrowser] 🚀 Transitioning to LobbyRoom panel");
                    _uiManager.ShowPanel(PanelID.LobbyRoom);
                }
                else
                {
                    UpdateStatusText("Failed to join room", MessageType.Error);
                    Debug.LogError("[LobbyBrowser] ❌ Failed to join room");
            
                    await System.Threading.Tasks.Task.Delay(2000);
                    
                    if (joinButton != null)
                        joinButton.SetInteractable(true);
                        
                    _browserState.IsJoiningRoom = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyBrowser] ❌ Exception joining room: {e.Message}");
                UpdateStatusText("Error joining room", MessageType.Error);
        
                if (joinButton != null)
                    joinButton.SetInteractable(true);
                    
                _browserState.IsJoiningRoom = false;
            }
        }
        
        #endregion
        
        #region UI State Management
        
        private void ShowLoadingState()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
                
                loadingIndicator.transform.DORotate(new Vector3(0, 0, -360), 2f, RotateMode.FastBeyond360)
                    .SetLoops(-1, LoopType.Restart)
                    .SetEase(Ease.Linear);
            }
            
            if (noRoomsMessage != null)
                noRoomsMessage.SetActive(false);
                
            UpdateStatusText("Searching for rooms...", MessageType.Info);
        }
        
        private void ShowNoRoomsState()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
                loadingIndicator.transform.DOKill();
            }
            
            if (noRoomsMessage != null)
                noRoomsMessage.SetActive(true);
                
            UpdateStatusText("No rooms available", MessageType.Warning);
        }
        
        private void UpdateStatusDisplay()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
                loadingIndicator.transform.DOKill();
            }
            
            if (_currentSessions.Count == 0)
            {
                if (noRoomsMessage != null)
                    noRoomsMessage.SetActive(true);
                    
                UpdateStatusText("No rooms available", MessageType.Warning);
            }
            else
            {
                if (noRoomsMessage != null)
                    noRoomsMessage.SetActive(false);
                    
                UpdateStatusText($"Found {_currentSessions.Count} room(s)", MessageType.Success);
            }
        }
        
        private void UpdateStatusText(string text, MessageType type)
        {
            if (statusText != null)
            {
                statusText.text = text;
                
                switch (type)
                {
                    case MessageType.Success:
                    case MessageType.Info:
                        statusText.color = colorTheme.TextWhite;
                        break;
                    case MessageType.Warning:
                    case MessageType.Error:
                        statusText.color = colorTheme.SecondaryGray;
                        break;
                }
                
                statusText.DOFade(0f, 0f);
                statusText.DOFade(1f, 0.3f);
            }
        }
        
        private enum MessageType
        {
            Info,
            Success,
            Warning,
            Error
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Actualiza una sesión específica en tiempo real
        /// </summary>
        public void UpdateSessionRealtime(SessionInfo updatedSession)
        {
            if (updatedSession == null || string.IsNullOrEmpty(updatedSession.Name))
                return;
                
            // Actualizar cache
            _browserState.CachedSessions[updatedSession.Name] = updatedSession;
            
            // Buscar y actualizar el item correspondiente
            var item = _roomItems.FirstOrDefault(i => 
                i.gameObject.activeSelf && 
                i.GetSessionInfo()?.Name == updatedSession.Name);
                
            if (item != null)
            {
                item.UpdateRealtimeData(updatedSession.PlayerCount, updatedSession.IsOpen);
            }
            
            // Si es la sala seleccionada, actualizar info
            if (_browserState.SelectedSessionName == updatedSession.Name)
            {
                _browserState.SelectedSession = updatedSession;
                OnRoomSelected(updatedSession);
            }
        }
        
        /// <summary>
        /// Fuerza un refresh manual
        /// </summary>
        public void ForceRefresh()
        {
            RefreshRoomList(true);
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Debug: Print Browser State")]
        private void DebugPrintState()
        {
            Debug.Log("=== LobbyBrowser State ===");
            Debug.Log($"Has Initial Load: {_browserState.HasInitialLoad}");
            Debug.Log($"Is Refreshing: {_browserState.IsRefreshing}");
            Debug.Log($"Is Joining: {_browserState.IsJoiningRoom}");
            Debug.Log($"Selected Room: {_browserState.SelectedSessionName ?? "None"}");
            Debug.Log($"Cached Sessions: {_browserState.CachedSessions.Count}");
            Debug.Log($"Consecutive Failures: {_browserState.ConsecutiveRefreshFailures}");
            Debug.Log($"Last Refresh: {Time.time - _browserState.LastRefreshTime}s ago");
            Debug.Log("==========================");
        }
        
        [ContextMenu("Debug: Force Refresh")]
        private void DebugForceRefresh()
        {
            ForceRefresh();
        }
        
        [ContextMenu("Debug: Clear Selection")]
        private void DebugClearSelection()
        {
            _browserState.SelectedSessionName = null;
            _browserState.SelectedSession = null;
            HandleSelectedRoomDisappeared();
        }
        
        #endregion
        
        private void OnDestroy()
        {
            if (_autoRefreshCoroutine != null)
            {
                StopCoroutine(_autoRefreshCoroutine);
            }
            
            // Limpiar tweens
            foreach (var item in _roomItems)
            {
                if (item != null && item.transform != null)
                    item.transform.DOKill();
            }
            
            if (loadingIndicator != null)
                loadingIndicator.transform.DOKill();
                
            if (selectedRoomInfo != null)
                selectedRoomInfo.transform.DOKill();
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using DG.Tweening;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;

namespace HackMonkeys.UI.Panels
{
    /// <summary>
    /// Panel VR para navegar y unirse a salas disponibles
    /// </summary>
    public class LobbyBrowser : MenuPanel
    {
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
        
        private NetworkBootstrapper _networkBootstrapper;
        private List<RoomItem> _roomItems = new List<RoomItem>();
        private List<SessionInfo> _currentSessions = new List<SessionInfo>();
        private SessionInfo _selectedSession;
        private bool _isRefreshing = false;
        
        private bool _showFullRooms = true;
        private bool _showInProgressRooms = true;
        private SortType _currentSortType = SortType.PlayerCount;
        
        private enum SortType
        {
            Name,
            PlayerCount,
            Ping
        }
        
        protected override void SetupPanel()
        {
            base.SetupPanel();
            
            _networkBootstrapper = NetworkBootstrapper.Instance;
            
            ConfigureButtons();
            
            ConfigureFilters();
            
            if (selectedRoomInfo != null)
                selectedRoomInfo.SetActive(false);
                
            InitializeRoomItemPool();
        }
        
        private void ConfigureButtons()
        {
            if (refreshButton != null)
            {
                refreshButton.OnButtonPressed.AddListener(RefreshRoomList);
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
                    //SortRooms();
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
                    roomItem.Initialize(i, OnRoomSelected);
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
                _networkBootstrapper.OnSessionListUpdatedEvent.AddListener(OnSessionListUpdated);
            }
    
            RefreshRoomList();
    
            InvokeRepeating(nameof(RefreshRoomList), 5f, 5f);
        }
        
        public override void OnPanelHidden()
        {
            base.OnPanelHidden();
    
            CancelInvoke(nameof(RefreshRoomList));
    
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnSessionListUpdatedEvent.RemoveListener(OnSessionListUpdated);
            }
        }
        
        private async void RefreshRoomList()
        {
            if (_isRefreshing) return;
    
            _isRefreshing = true;
    
            ShowLoadingState();
    
            if (refreshButton != null)
            {
                refreshButton.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360).SetEase(Ease.Linear);
            }
    
            try
            {
                Debug.Log("[LobbyBrowser] 🔍 Requesting session list from NetworkBootstrapper...");
        
                var sessions = await _networkBootstrapper.GetAvailableSessions();
        
                Debug.Log($"[LobbyBrowser] 📋 Received {sessions?.Count ?? 0} sessions");
        
                OnSessionListUpdated(sessions ?? new List<SessionInfo>());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyBrowser] ❌ Error getting sessions: {e.Message}");
                UpdateStatusText("Error loading rooms");
            }
            finally
            {
                _isRefreshing = false;
            }
        }
        
        private System.Collections.IEnumerator RefreshRoutine()
        {
            _isRefreshing = true;
            
            ShowLoadingState();
            
            if (refreshButton != null)
            {
                refreshButton.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear);
            }
            
            yield return new WaitForSeconds(0.5f);
            
            
            _isRefreshing = false;
        }
        
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
                    sessions.Sort((a, b) => a.Name.CompareTo(b.Name));
                    break;
                    
                case SortType.PlayerCount:
                    sessions.Sort((a, b) => b.PlayerCount.CompareTo(a.PlayerCount));
                    break;
                    
                case SortType.Ping:
                    // TODO: Implementar ordenamiento por ping cuando esté disponible
                    break;
            }
        }
        
        private void DisplayRooms(List<SessionInfo> sessions)
        {
            foreach (var item in _roomItems)
            {
                item.gameObject.SetActive(false);
            }
            
            int displayCount = Mathf.Min(sessions.Count, _roomItems.Count);
            
            for (int i = 0; i < displayCount; i++)
            {
                RoomItem roomItem = _roomItems[i];
                SessionInfo session = sessions[i];
                
                roomItem.SetRoomData(session);
                roomItem.gameObject.SetActive(true);
                
                // float targetY = -i * roomItemSpacing;
                // roomItem.transform.localPosition = new Vector3(0, targetY + 0.5f, 0);
                // roomItem.transform.DOLocalMoveY(targetY, 0.3f).SetDelay(i * 0.05f).SetEase(Ease.OutBack);
            }
            
            if (roomListContainer != null)
            {
                float containerHeight = displayCount * roomItemSpacing;
                roomListContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(roomListContainer.GetComponent<RectTransform>().sizeDelta.x, containerHeight);
            }
        }
        
        private void OnRoomSelected(SessionInfo session)
        {
            _selectedSession = session;
            
            if (selectedRoomInfo != null)
            {
                selectedRoomInfo.SetActive(true);
                
                selectedRoomInfo.transform.localScale = Vector3.zero;
                selectedRoomInfo.transform.DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
            }
            
            if (selectedRoomName != null)
                selectedRoomName.text = session.Name;
                
            if (selectedRoomPlayers != null)
                selectedRoomPlayers.text = $"{session.PlayerCount}/{session.MaxPlayers} Players";
                
            if (selectedRoomStatus != null)
            {
                string status = session.IsOpen ? "<color=green>Open</color>" : "<color=yellow>In Progress</color>";
                selectedRoomStatus.text = status;
            }
            
            if (joinButton != null)
            {
                bool canJoin = session.PlayerCount < session.MaxPlayers && session.IsOpen;
                joinButton.SetInteractable(canJoin);
            }
        }
        
        private async void JoinSelectedRoom()
        {
            if (_selectedSession == null || _networkBootstrapper == null) return;
    
            Debug.Log($"[LobbyBrowser] 🎮 Attempting to join room: {_selectedSession.Name}");
    
            if (joinButton != null)
                joinButton.SetInteractable(false);
        
            UpdateStatusText("Joining room...");
    
            try
            {
                bool success = await _networkBootstrapper.JoinRoom(_selectedSession);
        
                Debug.Log($"[LobbyBrowser] Join result: {success}");
        
                if (success)
                {
                    UpdateStatusText("Connected! Loading lobby...");
            
                    Debug.Log("[LobbyBrowser] ⏳ Waiting for player spawn...");
                    await System.Threading.Tasks.Task.Delay(1000);
            
                    Debug.Log("[LobbyBrowser] 🚀 Transitioning to LobbyRoom panel");
                    _uiManager.ShowPanel(PanelID.LobbyRoom);
                }
                else
                {
                    UpdateStatusText("Failed to join room");
                    Debug.LogError("[LobbyBrowser] ❌ Failed to join room");
            
                    await System.Threading.Tasks.Task.Delay(2000);
                    if (joinButton != null)
                        joinButton.SetInteractable(true);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyBrowser] ❌ Exception joining room: {e.Message}");
                UpdateStatusText("Error joining room");
        
                if (joinButton != null)
                    joinButton.SetInteractable(true);
            }
        }
        
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
                
            UpdateStatusText("Searching for rooms...");
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
                    
                UpdateStatusText("No rooms available");
            }
            else
            {
                if (noRoomsMessage != null)
                    noRoomsMessage.SetActive(false);
                    
                UpdateStatusText($"Found {_currentSessions.Count} room(s)");
            }
        }
        
        private void UpdateStatusText(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
                
                statusText.DOFade(0f, 0f);
                statusText.DOFade(1f, 0.3f);
            }
        }
    }
}
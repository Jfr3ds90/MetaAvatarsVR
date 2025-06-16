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
        [SerializeField] private InteractableButton3D backButton;
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
        
        // Filtros
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
            
            // Configurar botones
            ConfigureButtons();
            
            // Configurar filtros
            ConfigureFilters();
            
            // Ocultar info de sala seleccionada inicialmente
            if (selectedRoomInfo != null)
                selectedRoomInfo.SetActive(false);
                
            // Inicializar pool de items de sala
            InitializeRoomItemPool();
        }
        
        private void ConfigureButtons()
        {
            if (refreshButton != null)
            {
                refreshButton.OnButtonPressed.AddListener(RefreshRoomList);
            }
            
            if (backButton != null)
            {
                backButton.OnButtonPressed.AddListener(() => _uiManager.GoBack());
            }
            
            if (createRoomButton != null)
            {
                createRoomButton.OnButtonPressed.AddListener(() => _uiManager.ShowPanel(PanelID.CreateRoom));
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
            // Pre-crear items de sala para mejor performance
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
            
            // Suscribirse a eventos
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnSessionListUpdated_event.AddListener(OnSessionListUpdated);
            }
            
            // Refrescar lista automáticamente
            RefreshRoomList();
        }
        
        public override void OnPanelHidden()
        {
            base.OnPanelHidden();
            
            // Desuscribirse de eventos
            if (_networkBootstrapper != null)
            {
                _networkBootstrapper.OnSessionListUpdated_event.RemoveListener(OnSessionListUpdated);
            }
        }
        
        private void RefreshRoomList()
        {
            if (_isRefreshing) return;
            
            StartCoroutine(RefreshRoutine());
        }
        
        private System.Collections.IEnumerator RefreshRoutine()
        {
            _isRefreshing = true;
            
            // Mostrar estado de carga
            ShowLoadingState();
            
            // Animar botón de refresh
            if (refreshButton != null)
            {
                refreshButton.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear);
            }
            
            // Esperar un momento para feedback visual
            yield return new WaitForSeconds(0.5f);
            
            // La actualización real ocurre en OnSessionListUpdated
            
            _isRefreshing = false;
        }
        
        private void OnSessionListUpdated(List<SessionInfo> sessions)
        {
            _currentSessions = sessions ?? new List<SessionInfo>();
            
            // Aplicar filtros y mostrar
            ApplyFilters();
            
            // Actualizar estado
            UpdateStatusDisplay();
        }
        
        private void ApplyFilters()
        {
            List<SessionInfo> filteredSessions = new List<SessionInfo>();
            
            foreach (var session in _currentSessions)
            {
                // Filtrar salas llenas
                if (!_showFullRooms && session.PlayerCount >= session.MaxPlayers)
                    continue;
                    
                // Filtrar salas en progreso
                if (!_showInProgressRooms && session.IsOpen == false)
                    continue;
                    
                filteredSessions.Add(session);
            }
            
            // Ordenar
            SortSessions(filteredSessions);
            
            // Mostrar
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
            // Ocultar todos los items
            foreach (var item in _roomItems)
            {
                item.gameObject.SetActive(false);
            }
            
            // Mostrar salas disponibles
            int displayCount = Mathf.Min(sessions.Count, _roomItems.Count);
            
            for (int i = 0; i < displayCount; i++)
            {
                RoomItem roomItem = _roomItems[i];
                SessionInfo session = sessions[i];
                
                // Configurar item
                roomItem.SetRoomData(session);
                roomItem.gameObject.SetActive(true);
                
                // Posicionar con animación
                float targetY = -i * roomItemSpacing;
                roomItem.transform.localPosition = new Vector3(0, targetY + 0.5f, 0);
                roomItem.transform.DOLocalMoveY(targetY, 0.3f)
                    .SetDelay(i * 0.05f)
                    .SetEase(Ease.OutBack);
            }
            
            // Actualizar altura del contenedor para scroll
            if (roomListContainer != null)
            {
                float containerHeight = displayCount * roomItemSpacing;
                roomListContainer.GetComponent<RectTransform>().sizeDelta = 
                    new Vector2(roomListContainer.GetComponent<RectTransform>().sizeDelta.x, containerHeight);
            }
        }
        
        private void OnRoomSelected(SessionInfo session)
        {
            _selectedSession = session;
            
            // Mostrar información de la sala
            if (selectedRoomInfo != null)
            {
                selectedRoomInfo.SetActive(true);
                
                // Animar entrada
                selectedRoomInfo.transform.localScale = Vector3.zero;
                selectedRoomInfo.transform.DOScale(Vector3.one, 0.3f)
                    .SetEase(Ease.OutBack);
            }
            
            // Actualizar textos
            if (selectedRoomName != null)
                selectedRoomName.text = session.Name;
                
            if (selectedRoomPlayers != null)
                selectedRoomPlayers.text = $"{session.PlayerCount}/{session.MaxPlayers} Players";
                
            if (selectedRoomStatus != null)
            {
                string status = session.IsOpen ? "<color=green>Open</color>" : "<color=yellow>In Progress</color>";
                selectedRoomStatus.text = status;
            }
            
            // Habilitar botón de unirse si la sala no está llena
            if (joinButton != null)
            {
                bool canJoin = session.PlayerCount < session.MaxPlayers && session.IsOpen;
                joinButton.SetInteractable(canJoin);
            }
        }
        
        private async void JoinSelectedRoom()
        {
            if (_selectedSession == null || _networkBootstrapper == null) return;
            
            // Deshabilitar botón
            if (joinButton != null)
                joinButton.SetInteractable(false);
                
            // Mostrar estado de conexión
            UpdateStatusText("Joining room...");
            
            // Intentar unirse
            bool success = await _networkBootstrapper.JoinRoom(_selectedSession);
            
            if (success)
            {
                UpdateStatusText("Connected! Loading game...");
                // La transición a la escena del juego se maneja en NetworkBootstrapper
            }
            else
            {
                UpdateStatusText("Failed to join room");
                
                // Rehabilitar botón después de un momento
                await System.Threading.Tasks.Task.Delay(2000);
                if (joinButton != null)
                    joinButton.SetInteractable(true);
            }
        }
        
        private void ShowLoadingState()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
                
                // Animar indicador de carga
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
                
                // Efecto de fade in
                statusText.DOFade(0f, 0f);
                statusText.DOFade(1f, 0.3f);
            }
        }
    }
}
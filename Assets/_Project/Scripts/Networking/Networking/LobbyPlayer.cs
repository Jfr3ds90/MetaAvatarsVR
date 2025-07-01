using UnityEngine;
using Fusion;
using TMPro;

namespace HackMonkeys.Core
{
    /// <summary>
    /// Representa a un jugador en el lobby con datos sincronizados en red
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        [Networked]
        public NetworkString<_32> PlayerName { get; set; }
        
        [Networked]
        public NetworkBool IsReady { get; set; }
        
        [Networked]
        public NetworkBool IsHost { get; set; }
        
        [Networked]
        public Color PlayerColor { get; set; }
        
        // Referencias locales (no sincronizadas)
        private PlayerPrefsManager _prefsManager;
        private ChangeDetector _changeDetector;
        
        // Cache de valores anteriores para detectar cambios
        private NetworkString<_32> _previousName;
        private NetworkBool _previousReady;
        
        public PlayerRef PlayerRef => Object.InputAuthority;
        public bool IsLocalPlayer => HasInputAuthority;
        
        public override void Spawned()
        {
            // Inicializar el ChangeDetector
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            
            // Si es nuestro jugador local
            if (HasInputAuthority)
            {
                _prefsManager = PlayerPrefsManager.Instance;
                
                // Configurar datos iniciales
                RPC_SetPlayerData(
                    _prefsManager.GetPlayerName(),
                    _prefsManager.GetPlayerColor(),
                    Runner.IsServer // Es host si es el servidor
                );
            }
            
            // Registrar en el LobbyManager
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.RegisterPlayer(this);
            }
            
            // Guardar valores iniciales
            _previousName = PlayerName;
            _previousReady = IsReady;
        }
        
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Desregistrar del LobbyManager
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.UnregisterPlayer(this);
            }
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_SetPlayerData(NetworkString<_32> name, Color color, NetworkBool isHost)
        {
            PlayerName = name;
            PlayerColor = color;
            IsHost = isHost;
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(NetworkBool ready)
        {
            IsReady = ready;
        }
        
        public override void FixedUpdateNetwork()
        {
            // Detectar cambios usando ChangeDetector
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(PlayerName):
                        OnNameChangedCallback();
                        break;
                    case nameof(IsReady):
                        OnReadyChangedCallback();
                        break;
                }
            }
        }
        
        private void OnNameChangedCallback()
        {
            Debug.Log($"[LobbyPlayer] Name changed to: {PlayerName.ToString()}");
            
            // Notificar al UI
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.UpdatePlayerDisplay(this);
            }
        }
        
        private void OnReadyChangedCallback()
        {
            Debug.Log($"[LobbyPlayer] Ready state changed to: {IsReady}");
            
            // Notificar al UI
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.UpdatePlayerDisplay(this);
            }
        }
        
        // Métodos públicos para el UI
        public void ToggleReady()
        {
            if (HasInputAuthority)
            {
                RPC_SetReady(!IsReady);
            }
        }
        
        public string GetDisplayName()
        {
            string name = PlayerName.ToString();
            if (string.IsNullOrEmpty(name))
                name = "Player";
                
            if (IsHost)
                name += " (Host)";
                
            return name;
        }
        
        #region Debug
        
        private void OnGUI()
        {
            if (!Application.isEditor) return;
            
            if (HasInputAuthority)
            {
                GUILayout.Label($"Local Player: {PlayerName} - Ready: {IsReady}");
            }
        }
        
        #endregion
    }
}
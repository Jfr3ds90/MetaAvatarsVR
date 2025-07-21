using System;
using UnityEngine;
using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using TMPro;

public class Create : MenuPanel
{
    [SerializeField] TMP_InputField roomNameField;
    [SerializeField] TMP_InputField roomPasswordField;
    [SerializeField] InteractableButton3D createRoomButton;
    [SerializeField] private NetworkBootstrapper networkBootstrapper;
    
    private GameCore _gameCore;

    private void Awake()
    {
        if (networkBootstrapper == null)
        {
            networkBootstrapper = NetworkBootstrapper.Instance;
        }
        createRoomButton.OnButtonPressed.AddListener(CreateRoom);
    }

    private void Start()
    {
        _gameCore = GameCore.Instance;
    }

    private async void CreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameField.text))
        {
            Debug.LogWarning("Room name cannot be empty!");
            return;
        }
        
        if (createRoomButton != null) createRoomButton.SetInteractable(false);
        
        //Todo: Actualizar el maxPlayers 
        bool success = await networkBootstrapper.CreateRoom(roomNameField.text, 4); 
        
        if (success)
        {
            _gameCore.OnJoinedLobby(roomNameField.text, true);
            
            await System.Threading.Tasks.Task.Delay(1000); 
            _uiManager.ShowPanel(PanelID.LobbyRoom);
        }
        else
        {
            Debug.LogError("Failed to create room!");
            
            if (createRoomButton != null) createRoomButton.SetInteractable(true);
        }
    }

    public override void OnPanelShown()
    {
        base.OnPanelShown();
        roomNameField.text = string.Empty;
        roomPasswordField.text = string.Empty;
    }
}

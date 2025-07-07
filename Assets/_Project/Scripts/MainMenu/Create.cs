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

    private void Awake()
    {
        if (networkBootstrapper == null)
        {
            networkBootstrapper = NetworkBootstrapper.Instance;
        }
        createRoomButton.OnButtonPressed.AddListener(CreateRoom);
    }

    private async void CreateRoom()
    {
        // Validar entrada
        if (string.IsNullOrEmpty(roomNameField.text))
        {
            Debug.LogWarning("Room name cannot be empty!");
            return;
        }
        
        // Deshabilitar el botón mientras se crea
        if (createRoomButton != null)
            createRoomButton.SetInteractable(false);
        
        // Crear la sala
        bool success = await networkBootstrapper.CreateRoom(roomNameField.text, 4); // Max 4 jugadores
        
        if (success)
        {
            await System.Threading.Tasks.Task.Delay(1000); 
            // Transición al panel del lobby
            _uiManager.ShowPanel(PanelID.LobbyRoom);
        }
        else
        {
            // Error al crear sala
            Debug.LogError("Failed to create room!");
            
            // Rehabilitar el botón
            if (createRoomButton != null)
                createRoomButton.SetInteractable(true);
        }
    }

    public override void OnPanelShown()
    {
        base.OnPanelShown();
        roomNameField.text = string.Empty;
        roomPasswordField.text = string.Empty;
        //createRoomButton.SetInteractable(false);
    }
}

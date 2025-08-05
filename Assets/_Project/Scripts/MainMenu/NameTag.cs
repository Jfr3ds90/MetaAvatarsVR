using HackMonkeys.Core;
using HackMonkeys.UI.Spatial;
using UnityEngine;

public class NameTag : MenuPanel
{
    [Header("UI Elements")] 
    [SerializeField] private InteractableButton3D _buttonContinue;
    [SerializeField] private InteractableInputField3D _inputField;

    private string _tempPlayerName = "";
    private string _currentPlayerName = ""; // Nombre actual del jugador

    public override void OnPanelShown()
    {
        base.OnPanelShown();

        // Obtener el nombre actual del jugador
        if (PlayerDataManager.Instance != null)
        {
            _currentPlayerName = PlayerDataManager.Instance.GetPlayerName();
        }

        // Configurar el input field
        if (_inputField != null)
        {
            // Campo vacío pero con placeholder mostrando el nombre actual
            _inputField.SetText("");
            _inputField.SetPlaceholder(_currentPlayerName);
            
           
        }
        
        // El botón continuar debe estar habilitado porque ya hay un nombre válido
        if (_buttonContinue != null)
        {
            _buttonContinue.SetInteractable(true);
            _buttonContinue.SetButtonLabel("Continue"); // o "Keep current name"
        }
        
        _tempPlayerName = "";
    }

    protected override void ConfigureButtons()
    {
        base.ConfigureButtons();

        // Configurar el input field
        if (_inputField != null)
        {
            _inputField.OnValueChanged.RemoveAllListeners();
            _inputField.OnValueChanged.AddListener(text =>
            {
                _tempPlayerName = text;
                
                // Actualizar el botón según si hay texto nuevo o no
                /*if (_buttonContinue != null)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        // No hay texto nuevo, usar el nombre actual
                        _buttonContinue.SetInteractable(true);
                        _buttonContinue.SetButtonLabel($"Continue as {_currentPlayerName}");
                    }
                    else if (text.Length >= 2)
                    {
                        // Hay un nombre nuevo válido
                        _buttonContinue.SetInteractable(true);
                        _buttonContinue.SetButtonLabel($"Continue as {text}");
                    }
                    else
                    {
                        // Nombre nuevo pero muy corto
                        _buttonContinue.SetInteractable(false);
                        _buttonContinue.SetButtonLabel("Name too short");
                    }
                }*/
            });
            
            // Cuando termine de editar
            _inputField.OnEndEdit.RemoveAllListeners();
            _inputField.OnEndEdit.AddListener(text =>
            {
                // Si presiona Enter con campo vacío, mantener el nombre actual
                if (string.IsNullOrWhiteSpace(text))
                {
                    _tempPlayerName = "";
                }
            });
        }

        // Configurar el botón continuar
        if (_buttonContinue != null)
        {
            _buttonContinue.OnButtonPressed.RemoveAllListeners();
            _buttonContinue.OnButtonPressed.AddListener(() => 
            {
                string nameToSave = "";
                
                // Determinar qué nombre usar
                if (!string.IsNullOrWhiteSpace(_tempPlayerName) && _tempPlayerName.Length >= 2)
                {
                    // Usar el nombre nuevo
                    nameToSave = _tempPlayerName;
                }
                else
                {
                    // Mantener el nombre actual
                    nameToSave = _currentPlayerName;
                }
                
                // Solo guardar si es diferente al actual
                if (nameToSave != _currentPlayerName)
                {
                    PlayerDataManager.Instance.SetPlayerName(nameToSave);
                    Debug.Log($"[NameTag] Name changed from '{_currentPlayerName}' to '{nameToSave}'");
                }
                else
                {
                    Debug.Log($"[NameTag] Keeping current name: {nameToSave}");
                }
                
                // Ir al menú principal
                _uiManager.ShowPanel(PanelID.MainPanel);
            });
        }
    }
    
    public override void OnPanelHidden()
    {
        base.OnPanelHidden();
        
        // Limpiar listeners
        _inputField?.OnValueChanged.RemoveAllListeners();
        _inputField?.OnEndEdit.RemoveAllListeners();
        _buttonContinue?.OnButtonPressed.RemoveAllListeners();
    }
}
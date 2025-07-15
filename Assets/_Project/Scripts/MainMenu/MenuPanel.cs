using HackMonkeys.UI.Spatial;
using UnityEngine;

/// <summary>
/// Clase base para paneles de menú en VR
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class MenuPanel : MonoBehaviour
{
    private CanvasGroup _canvasGroup;
    protected SpatialUIManager _uiManager;
    public PanelID panelID;
    public InteractableButton3D backButton;

        
    public CanvasGroup CanvasGroup
    {
        get
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            return _canvasGroup;
        }
    }
        
    public virtual void Initialize(SpatialUIManager uiManager)
    {
        _uiManager = uiManager;
        SetupPanel();
    }
        
    protected virtual void SetupPanel()
    {
        ConfigureButtons();
    }
        
    public virtual void OnPanelShown()
    {
    }
        
    public virtual void OnPanelHidden()
    {
    }

    protected virtual void ConfigureButtons()
    {
        if (backButton != null)
        {
            backButton.OnButtonPressed.AddListener(() => _uiManager.GoBack());
        }
    }
}
using DG.Tweening;
using Fusion;
using HackMonkeys.UI.Spatial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente para items individuales de sala en la lista
/// </summary>
public class RoomItem : MonoBehaviour
{
    [Header("UI Elements")] [SerializeField]
    private TextMeshProUGUI roomNameText;

    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField] private Image statusIndicator;
    [SerializeField] private InteractableButton3D selectButton;
    [SerializeField] private Image backgroundImage;

    [Header("Colors")] 
    [SerializeField] private Color normalColor; 
    [SerializeField] private Color openColor = Color.green;
    [SerializeField] private Color closedColor = Color.yellow;
    [SerializeField] private Color fullColor = Color.red;
    [SerializeField] private Color selectedColor = new Color(0.2f, 0.4f, 0.8f, 0.5f);

    private SessionInfo _sessionInfo;
    private System.Action<SessionInfo> _onSelected;
    private int _index;
    private bool _isSelected = false;

    public void Initialize(int index, System.Action<SessionInfo> onSelected)
    {
        _index = index;
        _onSelected = onSelected;

        if (selectButton != null)
        {
            selectButton.OnButtonPressed.AddListener(OnSelectPressed);
        }
    }

    public void SetRoomData(SessionInfo session)
    {
        _sessionInfo = session;

        // Actualizar textos
        if (roomNameText != null)
            roomNameText.text = session.Name;

        if (playerCountText != null)
        {
            playerCountText.text = $"{session.PlayerCount}/{session.MaxPlayers}";

            // Cambiar color según capacidad
            if (session.PlayerCount >= session.MaxPlayers)
                playerCountText.color = fullColor;
            else if (session.PlayerCount > session.MaxPlayers * 0.75f)
                playerCountText.color = closedColor;
            else
                playerCountText.color = openColor;
        }

        if (pingText != null)
        {
            // TODO: Mostrar ping real cuando esté disponible
            pingText.text = "---ms";
        }

        // Actualizar indicador de estado
        if (statusIndicator != null)
        {
            if (!session.IsOpen)
                statusIndicator.color = closedColor;
            else if (session.PlayerCount >= session.MaxPlayers)
                statusIndicator.color = fullColor;
            else
                statusIndicator.color = openColor;
        }

        // Reset selección
        SetSelected(false);
    }

    private void OnSelectPressed()
    {
        _onSelected?.Invoke(_sessionInfo);
        SetSelected(true);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (backgroundImage != null)
        {
            Color targetColor = selected ? selectedColor : normalColor;
            backgroundImage.DOColor(targetColor, 0.2f);
        }
    }
}
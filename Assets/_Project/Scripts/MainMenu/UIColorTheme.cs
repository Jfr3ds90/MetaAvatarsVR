using UnityEngine;

namespace HackMonkeys.UI.Theme
{
    /// <summary>
    /// Sistema centralizado de colores para toda la UI del juego
    /// Mantiene consistencia visual y facilita cambios de tema
    /// </summary>
    [CreateAssetMenu(fileName = "UIColorTheme", menuName = "HackMonkeys/UI Color Theme")]
    public class UIColorTheme : ScriptableObject
    {
        [Header("Primary Colors - 3 Color Palette")]
        [SerializeField] private Color primaryYellow = new Color(1f, 0.92f, 0.016f, 1f); // #FFD700
        [SerializeField] private Color textWhite = Color.white; // #FFFFFF
        [SerializeField] private Color secondaryGray = new Color(0.5f, 0.5f, 0.5f, 1f); // #808080
        
        [Header("Alpha Variations")]
        [SerializeField] private float backgroundAlpha = 0.9f;
        [SerializeField] private float hoverAlpha = 0.85f;
        [SerializeField] private float selectedAlpha = 0.7f;
        [SerializeField] private float disabledAlpha = 0.5f;
        
        // Singleton para acceso global
        private static UIColorTheme _instance;
        public static UIColorTheme Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UIColorTheme>("UIColorTheme");
                    if (_instance == null)
                    {
                        Debug.LogWarning("UIColorTheme not found in Resources folder. Using default colors.");
                        _instance = CreateInstance<UIColorTheme>();
                    }
                }
                return _instance;
            }
        }
        
        // Propiedades de acceso
        public Color PrimaryYellow => primaryYellow;
        public Color TextWhite => textWhite;
        public Color SecondaryGray => secondaryGray;
        
        // Métodos helper para obtener colores con alpha
        public Color GetBackgroundColor(float alphaMultiplier = 1f)
        {
            Color color = primaryYellow;
            color.a = backgroundAlpha * alphaMultiplier;
            return color;
        }
        
        public Color GetHoverColor()
        {
            Color color = primaryYellow;
            color.a = backgroundAlpha * hoverAlpha;
            return color;
        }
        
        public Color GetSelectedColor()
        {
            Color color = primaryYellow;
            color.a = backgroundAlpha * selectedAlpha;
            return color;
        }
        
        public Color GetDisabledColor()
        {
            Color color = secondaryGray;
            color.a = disabledAlpha;
            return color;
        }
        
        // Métodos para estados de sala
        public Color GetRoomStateColor(bool isOpen, bool isFull)
        {
            if (!isOpen || isFull)
                return secondaryGray; // Cerrada o llena = gris
            else
                return textWhite; // Abierta = blanco
        }
        
        public Color GetPlayerCountColor(int current, int max)
        {
            float fillPercentage = (float)current / max;
            
            if (fillPercentage >= 1f)
                return secondaryGray; // Llena = gris
            else if (fillPercentage > 0.75f)
                return Color.Lerp(textWhite, secondaryGray, (fillPercentage - 0.75f) * 4f); // Transición
            else
                return textWhite; // Espacio disponible = blanco
        }
        
        public Color GetReadyStateColor(bool isReady)
        {
            return isReady ? textWhite : secondaryGray;
        }
        
        public Color GetPingColor(int ping)
        {
            if (ping < 50)
                return textWhite;
            else if (ping < 100)
                return Color.Lerp(textWhite, secondaryGray, (ping - 50f) / 50f);
            else
                return secondaryGray;
        }
    }
}
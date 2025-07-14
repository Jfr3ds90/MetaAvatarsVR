using TMPro;
using UnityEngine;

public class CanvasTasksShow : MonoBehaviour
{
    public static string level;
    public static int phase = 1;
    [SerializeField] private GameObject timePanel, instructionsPanel;
    public void ShowInstructions ()
    {
        switch(level)
        {
            case "oficina":
                timePanel.SetActive(!timePanel.activeSelf);
                instructionsPanel.SetActive(!instructionsPanel.activeSelf);
                switch(phase)
                {
                    case 1:
                            instructionsPanel.GetComponent<TMP_Text>().text = "Busca la manera de salir de la cocina";
                        break;
                    case 2:
                            instructionsPanel.GetComponent<TMP_Text>().text = "Busca la manera de volver a la sala de computadores";
                        break; 
                    case 3:
                        instructionsPanel.GetComponent<TMP_Text>().text = "Devuelve la luz a la oficina y busca como abrir el resto de puertas";
                        break; 
                    case 4:
                        instructionsPanel.GetComponent<TMP_Text>().text = "Busca el pendrive de respaldo";
                        break; 
                    case 5:
                        instructionsPanel.GetComponent<TMP_Text>().text = "¡Envía el archivo!";
                        break;
                    default: break;
                }               
                break;
            default: break;
        }
    }
}

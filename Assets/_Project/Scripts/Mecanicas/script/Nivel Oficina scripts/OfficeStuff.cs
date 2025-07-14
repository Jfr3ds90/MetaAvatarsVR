using UnityEngine;

public class OfficeStaff : MonoBehaviour
{
    bool lightsOn = false;
    public GameObject lightsObjects, emergencyLights,CanvasPc,ButtonsCanvas,pendrive,creditsEnd;

    public void CoffeMachine()
    {
        lightsOn = false;
        lightsObjects.SetActive(false);
        emergencyLights.SetActive(true);

    }
    public void lightBox()
    {
        lightsOn = true;
        lightsObjects.SetActive(true);
        emergencyLights.SetActive(true);
    }
    public void activationPc()
    {
        CanvasPc.SetActive(true);
    }
    public void CorrectOption()
    {
        creditsEnd.SetActive(true);//sacar cuando el video funcione
        pendrive.GetComponent<simpleKey>().videoCorrect = true;       
        ButtonsCanvas.SetActive(false);
        Debug.Log("opcion correcta");
    }
    public void IncorrectOption()
    {
        Debug.Log("opcion incorrecta");
    }
}

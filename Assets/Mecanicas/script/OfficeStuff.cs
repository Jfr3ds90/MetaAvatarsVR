using UnityEngine;

public class OfficeStaff : MonoBehaviour
{
    bool lightsOn = true;
    public GameObject lightsObjects, emergencyLights, ShelfKitchen;
    public GameObject[] leversObject;
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
    public void kitchenShelf()
    {
        var shelf = ShelfKitchen.GetComponent<Switch>();
        shelf.orientation = !shelf.orientation;
        shelf.OpenDoor(shelf.orientation);
    }
    public void kitchenLever() //misma lógica que el piano asi que se reutiliza
    {
        var leverOrder = GetComponent<Piano>();
       // leverOrder.partiture(leversObject.);
    }
}

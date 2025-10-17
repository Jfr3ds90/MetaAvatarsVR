using UnityEngine;
using HackMonkeys.Debugging;

public class PortalObjects : MonoBehaviour
{
    public Transform Destination;
    bool activation = false;
    public Switch door;
    [SerializeField]GameObject particles;
    private void OnTriggerStay(Collider other)
    {
        if (activation == true && other.tag == "cubePuzzle")
        {
            other.gameObject.transform.position = Destination.position;           
            AdvancedDebugSystem.Log(other.gameObject.name, LogCategory.Avatar, LogLevel.Debug);
            if (door != null)
                door.OpenDoorAct();
        }
        else
            activation = false;
            activation = false;
    }
    public void InterruptorButton()
    {
        activation = true;
        particles.SetActive(false);
    }
    public void whenIsSelected()
    {
        particles.SetActive(true);
    }
}

using UnityEngine;

public class PortalObjects : MonoBehaviour
{
    public Transform Destination;
    bool activation = false;
    public Switch door;

    private void Update()
    {
        /*if (Input.GetKeyUp(KeyCode.Space))
        {
            InterruptorButton();
        }*/
    }
    private void OnTriggerStay(Collider other)
    {
        if (activation == true && other.tag == "cubePuzzle")
        {
            other.gameObject.transform.position = Destination.position;           
            Debug.Log(other.gameObject.name);
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
    }
}

using UnityEngine;

public class PortalObjects : MonoBehaviour
{
    public Transform Destination;
    bool activation = false;

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            InterruptorButton();
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (activation == true)
        {
            other.gameObject.transform.position = Destination.position;
            activation = false;
            Debug.Log(other.gameObject.name);
        }
    }
    public void InterruptorButton()
    {
        activation = true;
    }
}

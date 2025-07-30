using UnityEngine;

public class HallwayDoor : MonoBehaviour
{
    [SerializeField] Switch Dfl, Dfd, Dbl, Dbd;
    private void OnTriggerEnter(Collider colider)
    {
        if (colider.gameObject.tag == "Player")
        {
            Dfl.OpenDoorAct();
            Dfd.CloseDoorAct();
            Dbl.RAnimation(false);
            Dbd.RAnimation(true);
        }
    }
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.D))
        {
            Dfl.OpenDoor();
            Dfd.CloseDoorAct();
            Dbl.RAnimation(false);
            Dbd.RAnimation(true);
        }
        if(Input.GetKeyUp(KeyCode.A))
        {
            Dbl.CloseDoorAct();
            Dbd.OpenDoor();
        }
    }
}

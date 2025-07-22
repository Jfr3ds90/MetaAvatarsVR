using UnityEngine;

public class HallwayDoor : MonoBehaviour
{
    [SerializeField] Switch Dfl, Dfd, Dbl, Dbd;
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            Dfl.OpenDoorAct();
            Dfd.CloseDoorAct();
            Dbl.CloseDoorAct();
            Dbd.OpenDoorAct();
        }
    }
}

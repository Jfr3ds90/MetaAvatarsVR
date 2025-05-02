using Meta.XR.ImmersiveDebugger.UserInterface;
using UnityEngine;

public class Switch : MonoBehaviour
{
    public Transform Door;
    public bool orientation;

    private void OnEnable()
    {
        if(Door==null)
            Door = GetComponent<Transform>();
    }
    private void Update()
    {
        if(orientation == true)
        {
            if (Door.rotation.y > 0)
                Door.rotation = Quaternion.Euler(Door.rotation.x,0,Door.rotation.z);
            else if (Door.rotation.y < (-90))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
        }
        else
        {
            if (Door.rotation.y > -90)
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
            else if (Door.rotation.y < (-180))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -180, Door.rotation.z);
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            orientation = !orientation;
            OpenDoor(orientation);
        }
        
    }
    public void OpenDoor(bool value)
    {
        if(value== true)
        Door.rotation = Quaternion.Euler(Door.rotation.x,(Door.rotation.y +90),Door.rotation.z) ;
        else
        Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y), Door.rotation.z);
    }
}

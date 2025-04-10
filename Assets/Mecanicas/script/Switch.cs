using UnityEngine;

public class Switch : MonoBehaviour
{
    public Transform Door;
    bool val;
    private void Update()
    {
        

        if(Input.GetKeyDown(KeyCode.S))
        {
            val = !val;
            OpenDoor(val);
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

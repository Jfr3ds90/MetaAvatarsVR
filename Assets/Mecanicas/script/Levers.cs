using UnityEngine;

public class Levers : MonoBehaviour
{
    public Piano piano;
    public int orderLevel;
    bool OnOff = false;
    public void actionLever()
    {
        if (OnOff == false)
        {
            transform.rotation = Quaternion.Euler(transform.rotation.x, (transform.rotation.y), transform.rotation.z + 60);
            OnOff = true;
            piano.partiture(orderLevel);
        }
        else
        {
            transform.rotation = Quaternion.Euler(transform.rotation.x, (transform.rotation.y), transform.rotation.z-60);
            OnOff =false;
            piano.partiture(-1);
        }
    }
}

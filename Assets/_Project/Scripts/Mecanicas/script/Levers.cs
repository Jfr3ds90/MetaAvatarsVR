using UnityEngine;

public class Levers : MonoBehaviour
{
    public Piano piano;
    public int orderLevel;
    bool OnOff = false;
    Transform transformThis;
    public float z;
    private void OnEnable()
    {
        transformThis=this.transform;
    }
    public void actionLever()
    {
        
        if (transformThis.rotation.z >= -0.55f && OnOff == false)//estaba abajo, suelta arriba y es correcto
        {
            OnOff = true;
            piano.partiture(orderLevel);
            Debug.Log("palanca activa");
            //FindAnyObjectByType<AudioManager>().FindLevers += 1;
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == false) //estaba abajo, suelta abajo
        {
            Debug.Log("palanca inactiva");
        }
        else if (transformThis.rotation.z >= -0.55f && OnOff == true)// estaba arriba y queda arriba
        { // no ocurre nada
            piano.partiture(orderLevel);
            Debug.Log("palanca activa");
            //FindAnyObjectByType<AudioManager>().FindLevers += 1;
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == true)// estaba arriba y queda abajo
        {
            OnOff = false;
            piano.partiture(-1);
            //FindAnyObjectByType<AudioManager>().FindLevers -= 1;
            FindAnyObjectByType<AudioManager>().FailLevers +=1;
            Debug.Log("palanca inactiva");
        }
        z = transform.rotation.z;
    }
}

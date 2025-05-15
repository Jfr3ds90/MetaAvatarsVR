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
    private void Update()
    {
        z = transform.rotation.z;
        //Debug.Log(transformThis.rotation.z);
        //if (transformThis.rotation.z <= -0.5f && OnOff == false)
        //{
        //    OnOff = true;
        //    piano.partiture(orderLevel);
        //    Debug.Log("palanca activa");
        //}
        //else if (transformThis.rotation.z !<= -0.5f && OnOff == true)
        //{
        //    OnOff = false;
        //    piano.partiture(-1);
        //    Debug.Log("palanca inactiva");
        //}
        //Debug.Log(transformThis.rotation.x + " " + transformThis.rotation.y + " " + transformThis.rotation.z);
        if (Input.GetKeyDown(KeyCode.E))
            actionLever();
    }
    public void actionLever()
    {
        
        if (transformThis.rotation.z >= -0.55f && OnOff == false)//estaba abajo, suelta arriba y es correcto
        {
            OnOff = true;
            piano.partiture(orderLevel);
            Debug.Log("palanca activa");
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == false) //estaba abajo, suelta abajo
        {
            Debug.Log("palanca inactiva");
        }
        else if (transformThis.rotation.z >= -0.55f && OnOff == true)// estaba arriba y queda arriba
        { // no ocurre nada
            Debug.Log("palanca activa");
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == true)// estaba arriba y queda abajo
        {
            OnOff = false;
            piano.partiture(-1);
            FindAnyObjectByType<AudioManager>().FailLevers +=1;
            Debug.Log("palanca inactiva");
        }
    }
}

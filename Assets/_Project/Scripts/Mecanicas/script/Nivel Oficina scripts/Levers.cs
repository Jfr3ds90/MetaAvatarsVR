using UnityEngine;
using HackMonkeys.Debugging;

public class Levers : MonoBehaviour
{
    [SerializeField]private Piano piano;
    [SerializeField] private int orderLevel;
    private bool OnOff = false;
    private Transform transformThis;
    [SerializeField] private float z;
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
            AdvancedDebugSystem.Log("palanca activa", LogCategory.Avatar, LogLevel.Debug);
            //FindAnyObjectByType<AudioManager>().FindLevers += 1;
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == false) //estaba abajo, suelta abajo
        {
            AdvancedDebugSystem.Log("palanca inactiva", LogCategory.Avatar, LogLevel.Debug);
        }
        else if (transformThis.rotation.z >= -0.55f && OnOff == true)// estaba arriba y queda arriba
        { // no ocurre nada
            piano.partiture(orderLevel);
            AdvancedDebugSystem.Log("palanca activa", LogCategory.Avatar, LogLevel.Debug);
            //FindAnyObjectByType<AudioManager>().FindLevers += 1;
        }
        else if (transformThis.rotation.z! <= -0.55f && OnOff == true)// estaba arriba y queda abajo
        {
            OnOff = false;
            piano.partiture(-1);
            //FindAnyObjectByType<AudioManager>().FindLevers -= 1;
            FindAnyObjectByType<AudioManager>().FailLevers +=1;
            AdvancedDebugSystem.Log("palanca inactiva", LogCategory.Avatar, LogLevel.Debug);
        }
        z = transform.rotation.z;
    }
}

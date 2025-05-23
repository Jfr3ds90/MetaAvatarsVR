using UnityEngine;

public class AreaDetectorAudio : MonoBehaviour
{
    public int phase,extra;//phase sirve para saberde que fase es, es solo para lectura
    private void OnTriggerEnter(Collider other)
    {
        var audio= FindAnyObjectByType<AudioManager>();

        if(other.tag=="Player")
        {
            audio.action = extra;
            audio.colide = true; audio.calls();
        }
        
    }
    private void OnTriggerExit(Collider other)
    { 
        var audio = FindAnyObjectByType<AudioManager>();
        if (other.tag == "Player")           
        audio.colide = false;
        if (/*de fase 1*/ audio.usedL==true&& phase==0||/*de fase 2*/ audio.ActualPhase==1 && phase == 1 ||/*de fase 3*/audio.ActualPhase == 2 && phase == 2)
        this.gameObject.SetActive(false);
    }
}

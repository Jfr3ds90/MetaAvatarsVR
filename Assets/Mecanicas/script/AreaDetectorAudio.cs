using UnityEngine;

public class AreaDetectorAudio : MonoBehaviour
{
    public int phase;
    private void OnTriggerEnter(Collider other)
    {
        var audio= FindAnyObjectByType<AudioManager>();
        audio.colide=true;
        if(other.tag=="Player")        
        switch (phase)
        {
                case 0:
                    
                    break;
                case 1:
                    audio.Phase1 = true;
                    break;
                case 2:
                    audio.Phase2 = true;
                    break;
                case 3:
                    audio.Phase3 = true; 
                    break;
            default:break;
        }
    }
    private void OnTriggerExit(Collider other)
    { 
        var audio = FindAnyObjectByType<AudioManager>();
        if (other.tag == "Player")           
        audio.colide = false;
    }
}

using UnityEngine;
using UnityEngine.Video;

public class AreaDetectorAudio : MonoBehaviour
{
    public int phase,extra;//phase sirve para saberde que fase es, es solo para lectura
    public bool notPlayer,idle;
    public Switch door;
   [HideInInspector]public bool activated1 = true;
    [SerializeField]float timer = 0;
    AudioManager manager;
    private void OnTriggerEnter(Collider other)
    {
        manager= FindAnyObjectByType<AudioManager>();

            if(other.tag=="Player"&& notPlayer==false && idle==false)
            {
                if (/*de fase 1*/ manager.usedL == true && phase == 0 ||
                manager.ActualPhase==phase)
                {
                manager.action = extra;
                manager.colide = true; manager.calls();

                manager.colide = false;
                    this.gameObject.SetActive(false);
                }
                if(phase > CanvasTasksShow.phase)
                CanvasTasksShow.phase = phase;
            }
        else if(notPlayer == true&&idle==false)
        {
            if(activated1==false)
            {
                FindAnyObjectByType<AudioManager>().moreAction = extra;
                FindAnyObjectByType<AudioManager>().ActualPhase = phase;
                FindAnyObjectByType<AudioManager>().calls();
               
                timer += Time.deltaTime;
                if(timer > 4) 
                {
                    FindAnyObjectByType<AudioManager>().moreAction = 3; FindAnyObjectByType<AudioManager>().calls();
                    activated1 = true; 
                }
            }

            
        }

    }
    private void OnTriggerExit(Collider other)
    {
        if(idle==true &&  other.gameObject.CompareTag("Player"))
            Destroy(this.gameObject);
    }
}

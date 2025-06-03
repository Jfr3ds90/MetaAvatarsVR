using UnityEngine;
using UnityEngine.Video;

public class AreaDetectorAudio : MonoBehaviour
{
    public int phase,extra;//phase sirve para saberde que fase es, es solo para lectura
    public bool notPlayer;
    public Switch door;
   [HideInInspector]public bool activated1 = true;
    [SerializeField]float timer = 0;
    private void OnTriggerEnter(Collider other)
    {
        var audio= FindAnyObjectByType<AudioManager>();

            if(other.tag=="Player"&& notPlayer==false)
            {
                if (/*de fase 1*/ audio.usedL == true && phase == 0 ||
                audio.ActualPhase==phase)
                {
                audio.action = extra;
                audio.colide = true; audio.calls();

                audio.colide = false;
                    this.gameObject.SetActive(false);
                }

            }
        else if(notPlayer == true)
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
    private void OnTriggerStay(Collider other)
    {
        if (notPlayer == true)
            if (activated1 == false)
                { 
                    timer += Time.deltaTime;
                    if (timer > 4)
                        {
                            FindAnyObjectByType<AudioManager>().moreAction = 3; FindAnyObjectByType<AudioManager>().calls();
                            activated1 = true;
                        }
            }
    }
}

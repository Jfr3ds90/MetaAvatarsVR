using UnityEngine;
using UnityEngine.Video;

public class simpleKey : MonoBehaviour
{
    public GameObject gameobjectInteractor;
    public bool right,pendrive,audioHeared ;
    public int phase, MAction;
    public void actionKey()
    {
      var animator = gameobjectInteractor.GetComponent<Animator>();
        //var sound = door.GetComponent<AudioSource>();
        if(right == true)
        {
    animator.SetBool("Close_", false);
        animator.SetBool("Right_", right);
        animator.SetTrigger("Activation_");
        }
        
        //if (sound != null)
        //    sound.Play(0);

    }
    private void OnCollisionEnter(Collision other)
    {
        Debug.Log("detecto");
        if (pendrive == false)
        {
            if(other.gameObject.name == "Mesh_Door_02 (7)"&& right == other.gameObject.GetComponent<Switch>().orientation 
            || other.gameObject.name == "Mesh_Door_02 (1)" && right == other.gameObject.GetComponent<Switch>().orientation)
            {
            other.gameObject.GetComponent<Switch>().OpenDoorAct();
            Debug.Log(other.gameObject+" detectado");
           // var val = other.GetComponent<AreaDetectorAudio>();
            //if (right == val.orientation)
                {
                Debug.Log("funciona!!");
              actionKey();
                }
            }
        }
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (pendrive == true&&other.GetComponent<AreaDetectorAudio>()!=null) 
        {
            if(other.GetComponent<AreaDetectorAudio>().phase==4)
            {
                FindAnyObjectByType<AudioManager>().ActualPhase = 5;
                FindAnyObjectByType<AudioManager>().moreAction = 2;
                FindAnyObjectByType<AudioManager>().calls();
                other.GetComponent<AreaDetectorAudio>().activated1 = false;
                gameobjectInteractor.GetComponent<VideoPlayer>().Play();
                if(other.GetComponent<AreaDetectorAudio>().activated1 == true)
                    { FindAnyObjectByType<AudioManager>().moreAction = 3; FindAnyObjectByType<AudioManager>().calls(); }
            }
        }
    }
    public void pickUpAudio()
    {
        if(audioHeared==false)
        {
            FindAnyObjectByType<AudioManager>().moreAction = MAction;
            FindAnyObjectByType<AudioManager>().ActualPhase = phase;
            FindAnyObjectByType<AudioManager>().calls();
            //if (!gameobjectInteractor.GetComponent<VideoPlayer>().isPlaying)
                audioHeared = true;
        }
    }
}

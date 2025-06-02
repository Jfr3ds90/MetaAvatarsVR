using UnityEngine;
using UnityEngine.Video;

public class simpleKey : MonoBehaviour
{
    public GameObject gameobjectInteractor;
    public bool right,pendrive ;
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
            if(other.GetComponent<AreaDetectorAudio>().phase==5)
            {
                gameobjectInteractor.GetComponent<VideoPlayer>().Play();
            }
        }
    }
}

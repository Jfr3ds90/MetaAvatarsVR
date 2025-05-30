using UnityEngine;

public class simpleKey : MonoBehaviour
{
    public GameObject door;
    public bool right ;
    public void actionKey()
    {
      var animator = door.GetComponent<Animator>();
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
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("detecto");
        if(other.GetComponent<AreaDetectorAudio>()!=null)
        {
            Debug.Log(other.gameObject+" detectado");
            var val = other.GetComponent<AreaDetectorAudio>();
            if (right == val.orientation)
            {
                Debug.Log("funciona!!");
              actionKey();
            }
        }
    }
}

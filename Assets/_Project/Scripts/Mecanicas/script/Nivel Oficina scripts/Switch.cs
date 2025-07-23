using Meta.XR.ImmersiveDebugger.UserInterface;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;

public class Switch : MonoBehaviour
{
    public Transform Door;
    public bool orientation;
    public MeshRenderer mat;
    bool onOff =false,activated=false;
    Animator animator;
    public int Phase;
    public int ActualMoreAction;
    public bool activationAudio;

    private void OnEnable()
    {
        if(Door==null)
            Door = GetComponent<Transform>();
        if(animator==null)
            animator = GetComponent<Animator>();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
            OpenDoorAct();    
    }
    public void OpenDoor()
    {
        animator.SetBool("Close_",false);
        animator.SetBool("Right_", orientation);
        animator.SetTrigger("Activation_");
    }
    public void OpenDoorAct()
    {
        changeColor();
        OpenDoor();Debug.Log(orientation+" orientacion");
        onOff = true;
        if (GetComponent<AudioSource>() != null)
            GetComponent<AudioSource>().Play(0);
        float timer=0;
        bool activationExtra = false;
        if(activationExtra==false)
        timer += Time.deltaTime;

        //Debug.Log("EL TIMER VA EN "+timer);
        if (timer >= 4)
        {
            if(activationAudio==true&&activated==false)
            {
                if(FindAnyObjectByType<AudioManager>()!=null)
                hearAudio(); 
                activated = true; }
            activationExtra = true;
        }
    }
    public void CloseDoorAct()
    {
        animator.SetBool("Close_", true);
        animator.SetBool("Right_", orientation);
        animator.SetTrigger("Activation_");
    }
    void orientationDoor(bool val)
    {
        if (val==true)
        {
            if (Door.rotation.y > 0)
                Door.rotation = Quaternion.Euler(Door.rotation.x, 0, Door.rotation.z);
            else if (Door.rotation.y < (-90))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
        }
        else if(val==false)
        {
            if (Door.rotation.y < -90)
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
            else if (Door.rotation.y > (-180))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -180, Door.rotation.z);
        }
    }
    public void changeColor()
    {
        if (mat!=null)
        {
        mat.materials[0].SetColor("_EmissionColor", Color.green);
        mat.materials[0].SetColor("_BaseColor", Color.green); 
        }
    }
    void hearAudio()
    {
       var value = FindAnyObjectByType<AudioManager>();
        value.ActualPhase = Phase;
        value.moreAction = ActualMoreAction;
    }
    public void RAnimation(bool openClose)
    {
        animator.SetBool("Close_", openClose);
        animator.SetBool("Right_", orientation);
        animator.SetTrigger("Repeated_");
    }
}

using Meta.XR.ImmersiveDebugger.UserInterface;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations;

public class Switch : MonoBehaviour
{
    public Transform Door;
    public bool orientation;
    public MeshRenderer mat;
    bool onOff =false;
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
       // if(orientation == true)
        //{
        //    if (Door.rotation.y > 0)
        //        Door.rotation = Quaternion.Euler(Door.rotation.x,0,Door.rotation.z);
        //    else if (Door.rotation.y > (-90))
        //        Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
        //}
      //  else
        //{

        //}


        if (Input.GetKeyDown(KeyCode.S))
        {
            //orientation = !orientation;
            //OpenDoor(orientation);
            OpenDoorAct();
        }
        
    }
    public void OpenDoor(bool value)
    {
        animator.SetBool("Close_",false);
        animator.SetBool("Right_",value);
        animator.SetTrigger("Activation_");

        //if (value == true)
        //{
            
        //    Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y - 90), Door.rotation.z);
        //    if (GetComponent<AudioSource>() != null)
        //        GetComponent<AudioSource>().Play();

        //    //Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y + 90), Door.rotation.z);
        //    orientationDoor(orientation);
        //    Debug.Log("la puerta esta " + orientation);
        //}
        //else if (value == false)
        //{ 
        //    Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y - 90), Door.rotation.z);
        //    orientationDoor(orientation);
        //}
    }
    public void OpenDoorAct()
    {
        changeColor();
        OpenDoor(orientation);Debug.Log(orientation+" orientacion");
        //if(orientation==true)
        //Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y - 90), Door.rotation.z);
        //else if(orientation==false)
        //    Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y + 90), Door.rotation.z);
        onOff = true;
        if (GetComponent<AudioSource>() != null)//revisar
            GetComponent<AudioSource>().Play(0);
        float timer=0;
        bool activationExtra = false;
        if(activationExtra==false)
        timer += Time.deltaTime;

        Debug.Log("EL TIMER VA EN "+timer);
        if (timer >= 4)
        {
            if(activationAudio==true)
            hearAudio();
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
}

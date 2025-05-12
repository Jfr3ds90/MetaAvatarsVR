using Meta.XR.ImmersiveDebugger.UserInterface;
using Unity.VisualScripting;
using UnityEngine;

public class Switch : MonoBehaviour
{
    public Transform Door;
    public bool orientation;
    public MeshRenderer mat;
    bool onOff =false;

    private void OnEnable()
    {
        if(Door==null)
            Door = GetComponent<Transform>();
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
        if (onOff == true)
        {
            if(orientation==false)
            {
                if (Door.rotation.y > -90)
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
                else if (Door.rotation.y < (-180))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -180, Door.rotation.z);
            }
            

            else if(orientation==true)
            {
                if (Door.rotation.y > 0)
                Door.rotation = Quaternion.Euler(Door.rotation.x,0,Door.rotation.z);
                else if (Door.rotation.y > (-90))
                Door.rotation = Quaternion.Euler(Door.rotation.x, -90, Door.rotation.z);
            }
        }

        //}


        if (Input.GetKeyDown(KeyCode.S))
        {
            //orientation = !orientation;
            OpenDoor(orientation);
        }
        
    }
    public void OpenDoor(bool value)
    {
        if(value== true)
        {
           var doorSound = GetComponent<AudioSource>();
            if (doorSound != null)
            {
                doorSound.Play();
            }
            Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y + 90), Door.rotation.z);
            orientation = !orientation;
            Debug.Log("la puerta esta " +orientation);
        }
        else if (value == false) 
        Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y), Door.rotation.z);
    }
    public void OpenDoorAct()
    {
        mat.materials[0].SetColor("_EmissionColor", Color.green);
        Door.rotation = Quaternion.Euler(Door.rotation.x, (Door.rotation.y - 90), Door.rotation.z);
        mat.materials[0].SetColor("_BaseColor", Color.green); ;
        onOff = true;
        var doorSound = GetComponent<AudioSource>();
        if (doorSound != null)       
            doorSound.Play();        
    }
}

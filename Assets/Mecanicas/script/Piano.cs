using UnityEngine;

public class Piano : MonoBehaviour
{
    public int[] notes;
    private int noteAction = 0;
    public Switch door;
    
    public GameObject action;
    public void partiture(int pressed)
    {
        for (int i = noteAction; i < notes.Length;) 
        {
            Debug.Log("Esta en la nota que suena "+ pressed +" Esta es la nota correcta "+ notes[i]);

            if (notes[i] == pressed)
            {
                noteAction += 1;
                if (i == notes.Length - 1) 
                {
                    Debug.Log("completo");
                    if (action !=null)
                    {
                        //if(door==null)
                          //  door=GetComponent<Switch>();

                        //door.OpenDoor(door.orientation);
                        door.OpenDoorAct();
                    }
                }
                break;
                ; }
            else
            {
                noteAction = 0;
                break;
            }                
                
           
        }
    }
}

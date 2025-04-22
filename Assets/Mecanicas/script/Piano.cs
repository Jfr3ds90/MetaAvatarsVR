using UnityEngine;

public class Piano : MonoBehaviour
{
    public int[] notes;
    private int noteAction = 0;
    
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
                      var actions =  action.GetComponent<Switch>();
                        actions.OpenDoor(actions.val);
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

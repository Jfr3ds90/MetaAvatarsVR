using UnityEngine;

public class Piano : MonoBehaviour
{
    public int[] notes;
    [SerializeField]private int noteAction = 0;
    public Switch door;

 /*   private void Update()
    {
        if(Input.GetKeyUp(KeyCode.Alpha0))
        partiture(0);
        if (Input.GetKeyUp(KeyCode.Alpha1))
            partiture(1);
        if (Input.GetKeyUp(KeyCode.Alpha2))
            partiture(2);
        if (Input.GetKeyUp(KeyCode.Alpha3))
            partiture(3);
        if (Input.GetKeyUp(KeyCode.Alpha4))
            partiture(4);

    }*/
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
                        door.OpenDoorAct();
                        FindAnyObjectByType<AudioManager>().ActualPhase = 1;
                        FindAnyObjectByType<AudioManager>().calls();
                        break;
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

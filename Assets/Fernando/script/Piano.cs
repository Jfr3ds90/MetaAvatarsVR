using UnityEngine;

public class Piano : MonoBehaviour
{
    public int[] notes;
    public void partiture(int pressed)
    {
        for (int i = 0; i < notes.Length;) 
        {
            Debug.Log("Esta en la nota que suena "+ pressed +" Esta es la nota correcta "+ notes[i]);

            if(notes[i] == pressed)
                i++;
            else 
            {
                i = 0;
                break; 
            }                

            if (i == notes.Length - 1)
                Debug.Log("completo");
        }
    }
}

using UnityEngine;

public class VictoryPuzzle : MonoBehaviour
{   
    public int puzzle,amount;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        switch(puzzle)
        {
            case 1:
                if(collision.collider.tag =="cubePuzzle")
                {   
                    Destroy(collision.gameObject);
                    amount++;
                    FindAnyObjectByType<Puzzle01>().EndLaberynth();                    
                }
                break;
            case 2:
                break;
            case 3:
                break;
            case 4:
                break;
            case 5:
                break;
        }    
    }
}

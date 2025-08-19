using UnityEngine;

public class VictoryPuzzle : MonoBehaviour
{   
    public int puzzle;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        switch(puzzle)
        {
            case 1:
                if(collision.collider.tag =="cubePuzzle")
                {   
                    Destroy(collision.gameObject);
                    FindAnyObjectByType<Puzzle01>().amount++;
                    FindAnyObjectByType<Puzzle01>().EndLaberynth();                    
                }
                break;
            case 2:
                break;
            case 3:
                break;
            case 4:
                if (collision.collider.tag == "cubePuzzle")
                {
                    var puz = FindAnyObjectByType<Puzzle04>();
                    puz.level++;
                    puz.kimbo.transform.position = puz.StartKimbo.position;
                    puz.ChangeLevel();
                }
                    break;
            case 5:
                break;
        }    
    }
}

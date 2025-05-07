using UnityEngine;

public class DetectorObjectPos : MonoBehaviour
{//lista/array de posiciones clave para resolver
 //si la posicion tiene el objeto indicado entonces queda true bool
 //cada objeto debe tener una ID para identificar en que pos va | cada objeto tendra su codigo con su bool
    public ObjectPos[] objectPos;
    public Switch door;
    public void orderSolution()
    {
        int totalCorrect = 0;
        for (int i = 0; i < objectPos.Length; i++) 
        {
            if (objectPos[i].correctPos == true)
            {
                totalCorrect++;
            }
            else 
            {

            }
        }
        if (totalCorrect == objectPos.Length)
        {
            Debug.Log("Correct placed order");
            door.OpenDoor(door.orientation);
        }
    }
}

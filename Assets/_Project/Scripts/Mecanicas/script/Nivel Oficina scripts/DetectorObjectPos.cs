using Unity.VisualScripting;
using UnityEngine;

public class DetectorObjectPos : MonoBehaviour
{//lista/array de posiciones clave para resolver
 //si la posicion tiene el objeto indicado entonces queda true bool
 //cada objeto debe tener una ID para identificar en que pos va | cada objeto tendra su codigo con su bool
    public ObjectPos[] objectPos;
    public Switch door;
    bool activated = false;
    public void orderSolution()
    {
        if(activated== false) 
        {
            int totalCorrect = 0;
            for (int i = 0; i < objectPos.Length; i++)          
                if (objectPos[i].correctPos == true)                
                    totalCorrect++;               
            
            if (totalCorrect == objectPos.Length && door != null)
            {
                Debug.Log("Correct placed order");
                if (door != null)
                    door.OpenDoorAct();
                FindAnyObjectByType<AudioManager>().ActualPhase = 2;
                activated = true;
            }
            else if (totalCorrect == objectPos.Length && door == null)
            {
                MeshRenderer meshRenderer = this.GetComponent<MeshRenderer>();
                meshRenderer.materials[0].SetColor("_EmissionColor", Color.green);
                meshRenderer.materials[0].SetColor("_BaseColor", Color.green);
            }
        }        
    }
    private void Update()
    {
       /* if(Input.GetKeyUp(KeyCode.Space))
        {
            MeshRenderer meshRenderer = this.GetComponent<MeshRenderer>();
            meshRenderer.materials[0].SetColor("_EmissionColor", Color.green);
            meshRenderer.materials[0].SetColor("_BaseColor", Color.green);
        }*/
    }
}

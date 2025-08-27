using UnityEngine;

public class ObjectKeyOrder : MonoBehaviour
{
    [SerializeField] private int id;// id objeto tiene que ser igual al idpos
    [SerializeField] 
    private Transform otherPos;
    private void OnTriggerEnter(Collider other)
    {
        if(other.GetComponent<ObjectPos>()!=null && id == other.GetComponent<ObjectPos>().IdPos)
        {
            other.GetComponent<ObjectPos>().correctPos = true;
            FindAnyObjectByType<DetectorObjectPos>().orderSolution();
            otherPos = other.transform;
            
        }
    }
    public void moveToPos()
    {
       transform.position = otherPos.position;
        transform.rotation = otherPos.rotation;
    }
}

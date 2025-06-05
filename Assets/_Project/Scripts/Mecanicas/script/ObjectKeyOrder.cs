using UnityEngine;

public class ObjectKeyOrder : MonoBehaviour
{
    [SerializeField] private int id;// id objeto tiene que ser igual al idpos
    Transform otherPos;
    private void OnTriggerEnter(Collider other)
    {
        if(id == other.GetComponent<ObjectPos>().IdPos)
        other.GetComponent<ObjectPos>().correctPos = true;
        FindAnyObjectByType<DetectorObjectPos>().orderSolution();
        otherPos = other.transform;
    }
    public void moveToPos()
    {
       transform.position = otherPos.position;
        transform.rotation = otherPos.rotation;
    }
}

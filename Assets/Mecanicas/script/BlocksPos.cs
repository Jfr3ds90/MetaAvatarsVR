using UnityEngine;

public class BlocksPos : MonoBehaviour
{
    void Start()
    {
        
    }
    void Update()
    {
        
    }

    void Detector()
    {
        var currentPointPosition = Quaternion.AngleAxis(0, transform.forward) * transform.forward;
        var currentPointPositionRight = Quaternion.AngleAxis(0, transform.right) * transform.forward;
        var currentPointPositionUp = Quaternion.AngleAxis(0, transform.up) * transform.forward;
        var currentPointPositionLeft = Quaternion.AngleAxis(0, -transform.right) * transform.forward;
        var currentPointPositionDown = Quaternion.AngleAxis(0, -transform.up) * transform.forward;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, currentPointPositionRight + currentPointPositionUp, out hit)
            || Physics.Raycast(transform.position, currentPointPositionLeft + currentPointPositionUp, out hit)
            || Physics.Raycast(transform.position, currentPointPositionRight + currentPointPositionDown, out hit)
            || Physics.Raycast(transform.position, currentPointPositionLeft + currentPointPositionDown, out hit))
        {

        }
    }
    void Figure()
    {
        //Colocar contador y dirección con base a lógica sobre donde esta colocado
    }
}

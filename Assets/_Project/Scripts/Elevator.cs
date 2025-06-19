using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    [SerializeField] private Transform elevator;//2.6 y -2.7

    private void Start()
    {
        elevator.position = transform.position;
    }
    public void elevatorMove(bool up)
    {
        StartCoroutine(elevatorMode(up));
    }
    IEnumerator elevatorMode(bool up)
    {
            if (up == true)
            {
                while (elevator.position.y < 2.6)
                {
                    elevator.position += new Vector3(0, Time.deltaTime, 0);
                    if (elevator.position.y > 2.6f)
                    {
                        elevator.position = new(elevator.position.x, 2.6f, elevator.position.z); ;
                    }
                    yield return null;
                }
            }
            else
            {
                while (elevator.position.y > 2.6)
                {
                    elevator.position -= new Vector3(0, Time.deltaTime, 0);
                    if (elevator.position.y < -2.7f)
                    {
                        elevator.position = new(elevator.position.x, -2.7f, elevator.position.z);
                    }
                    yield return null;
                }
            }
        
    }
}

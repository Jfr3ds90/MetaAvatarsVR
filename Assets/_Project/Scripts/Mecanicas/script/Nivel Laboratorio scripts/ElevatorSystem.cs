using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElevatorSystem : MonoBehaviour
{
    public GameObject elev;
    bool upDown,activation;
public void UpDownMovement()
    {
        upDown = !upDown;
        activation = true;
      //  StartCoroutine(movement());     no funciona  
    }
    //5,402107
    private void Update()
    {
    if (activation == true) 
    { 
        if (upDown == true)
        {
            elev.transform.position += new Vector3(0, -1f, 0) * Time.deltaTime;
            if (elev.transform.position.y <= -0.7f)
                activation = false;
        }
        else if (upDown == false)
        {
            elev.transform.position += new Vector3(0, 1f, 0) * Time.deltaTime;
            if (elev.transform.position.y >= 4.702107)
                activation = false;
        }
    }
}
    IEnumerator movement()
    {
        while (true) 
        {
            if (upDown==false) 
           { 
                elev.transform.position += new Vector3(0, 0.1f, 0) * Time.deltaTime;
                //if (elev.transform.position.y >= lastPos.position.y + 5.402107f)
                    yield return false;
            }
            else if (upDown == true)
           {
                elev.transform.position -= new Vector3(0, 0.1f, 0) * Time.deltaTime;
              //  if (elev.transform.position.y <= lastPos.position.y - 5.402107f)
                    yield return false;
            }
        }
    }
}

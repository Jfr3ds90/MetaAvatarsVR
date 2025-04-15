using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class RevealingEffect : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    private void Update()
    {
        Appear();           
    }

    
    public void Appear()
    {
        Light light;
        light = GetComponent<Light>();
        for (float i = 0; i < 15; i++)
        {
           // var currentPointPosition = Quaternion.AngleAxis(i, transform.forward) * transform.forward;
            var currentPointPositionRight = Quaternion.AngleAxis(i, transform.right) * transform.forward;
            var currentPointPositionUp = Quaternion.AngleAxis(i, transform.up) * transform.forward;
            var currentPointPositionLeft = Quaternion.AngleAxis(i, -transform.right) * transform.forward;
            var currentPointPositionDown = Quaternion.AngleAxis(i, -transform.up) * transform.forward;
            RaycastHit hit;
            if (Physics.Raycast(transform.position, currentPointPositionRight + currentPointPositionUp, out hit,light.range)
                || Physics.Raycast(transform.position, currentPointPositionLeft + currentPointPositionUp, out hit, light.range)
                || Physics.Raycast(transform.position, currentPointPositionRight + currentPointPositionDown, out hit, light.range)
                || Physics.Raycast(transform.position, currentPointPositionLeft + currentPointPositionDown, out hit, light.range))
            {
          
               if(hit.collider.gameObject == meshRenderer.gameObject)
                    meshRenderer.material.SetFloat("_Aparicion", meshRenderer.material.GetFloat("_Aparicion")+light.intensity);
                else
                {
                    meshRenderer.material.SetFloat("_Aparicion", -light.intensity);
                    if(meshRenderer.material.GetFloat("_Aparicion") <= 0)
                        meshRenderer.material.SetFloat("_Aparicion", 0) ;
                }
                    
            }




            //Debug.DrawRay(transform.position, currentPointPositionRight, Color.red, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionUp, Color.red, light.range);

            //Debug.DrawRay(transform.position, currentPointPositionRight + currentPointPositionUp, Color.green, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionLeft + currentPointPositionUp, Color.green, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionRight + currentPointPositionDown, Color.green, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionLeft + currentPointPositionDown, Color.green, light.range);

            //Debug.DrawRay(transform.position, currentPointPositionLeft, Color.red, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionDown, Color.red, light.range);
            //Debug.DrawRay(transform.position, currentPointPosition, UnityEngine.Color.blue, light.range);
            //Debug.DrawLine(transform.position, currentPointPosition);
            //  Debug.Log(light.range);


        }
    }
    public void changeLight() //cambio de color de linterna
    {
        Light light;
        light = GetComponent<Light>();
       
        if(light.color != Color.white)
        light.color = Color.white;

        else if(light.color != Color.magenta)
        light.color = Color.magenta;

    }
    //public float coneAngle = 45f; // The angle of the cone
    //public float distance = 10f;  // The distance of the raycast

    //void Update()
    //{
    //    float stepAngleDeg = coneAngle / 180f * Mathf.PI / 15; // angle between two sampled rays
    //    for (int i = 0; i < 15; i++)
    //    {
    //        Vector3 direction = Quaternion.Euler(0, 0, i * stepAngleDeg) * transform.forward;
    //        RaycastHit hit;
    //        if (Physics.Raycast(transform.position, direction, out hit, distance))
    //        {
    //            Debug.Log("Hit: " + hit.transform.name);
    //        }
    //        Debug.DrawRay(transform.position, direction, Color.red, distance);
    //    }
    //}

}

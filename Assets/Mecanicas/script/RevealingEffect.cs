using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class RevealingEffect : MonoBehaviour
{
    bool OnOff = false;
    private void Update()
    {
        Appear();
        if (Input.GetKeyUp(KeyCode.Q))
        changeLight();
        if (Input.GetKeyUp(KeyCode.R)) 
            OnOffLight();
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
                MeshRenderer meshRenderer = hit.collider.GetComponent<MeshRenderer>();
                    if (hit.collider.GetComponent<MeshRenderer>().material.name == "M_PistaUV")
                    meshRenderer.material.SetFloat("_Aparicion", meshRenderer.material.GetFloat("_Aparicion")+light.intensity);
                    else
                    {
                    meshRenderer.material.SetFloat("_Aparicion", -light.intensity);
                    if(meshRenderer.material.GetFloat("_Aparicion") <= 0)
                        meshRenderer.material.SetFloat("_Aparicion", 0) ;
                    }
                }
       
                    
            }
        
    }
    public void changeLight() //cambio de color de linterna
    {
        if (OnOff == true)
        {
            MeshRenderer meshRenderer;
            meshRenderer = GetComponentInParent<MeshRenderer>();
            Debug.Log(meshRenderer.materials[1].color + " es el color");
            if (meshRenderer.materials[1].color == Color.magenta || meshRenderer.materials[1].color == Color.black)
            {
                ; meshRenderer.materials[1].color = Color.white;
                meshRenderer.materials[1].SetColor("_EmissionColor", Color.white);
                this.GetComponent<Light>().color = Color.white;
            }
            else if (meshRenderer.materials[1].color == Color.white)
            {
                meshRenderer.materials[1].color = Color.magenta;
                meshRenderer.materials[1].SetColor("_EmissionColor", Color.magenta);
                this.GetComponent<Light>().color = Color.magenta;
            }
        }
            

    }
    public void OnOffLight()
    {
        MeshRenderer meshRenderer = GetComponentInParent<MeshRenderer>();
        if (meshRenderer.materials[1].color != Color.black)
        {
            meshRenderer.materials[1].color = Color.black;
            meshRenderer.materials[1].SetColor("_EmissionColor", Color.black);
            this.GetComponent<Light>().color = Color.black;
            OnOff = false;
        }
        else
        {
            meshRenderer.materials[1].color = Color.white;
            meshRenderer.materials[1].SetColor("_EmissionColor", Color.white);
            this.GetComponent<Light>().color = Color.white;
            OnOff = true;
        }
    }

}

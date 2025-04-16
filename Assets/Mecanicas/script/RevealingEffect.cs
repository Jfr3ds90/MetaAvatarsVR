
using UnityEngine;

public class RevealingEffect : MonoBehaviour
{
    bool OnOff = false;
    
    MeshRenderer LastMeshRenderer = null;
    private void Update()
    {
        if (OnOff== true)
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

        if (GetComponentInParent<MeshRenderer>().materials[1].color == Color.magenta)
        for (float i = 0; i < 15; i++)
        {
            var currentPointPosition = Quaternion.AngleAxis(i, transform.forward) * transform.forward;
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
                //Debug.Log("el material es "+ hit.collider.GetComponent<MeshRenderer>().material.name);
                if (hit.collider.GetComponent<MeshRenderer>().material.name == "M_PistaUV" || hit.collider.GetComponent<MeshRenderer>().material.name == "M_PistaUV (Instance)")
                { 
                    meshRenderer.material.SetFloat("_Aparicion", meshRenderer.material.GetFloat("_Aparicion") + light.intensity);
                        
                        meshRenderer.material.SetVector("_PosicionLuz", hit.point - hit.transform.position);// obtiene la posicion del rayo - posicion del objeto
                        Debug.Log(hit.point-hit.transform.position);
                    if (meshRenderer.material.GetFloat("_Aparicion") >= 1)
                        meshRenderer.material.SetFloat("_Aparicion", 1);

                    LastMeshRenderer = meshRenderer;
                }
                else if (hit.collider.GetComponent<MeshRenderer>().material.name != "M_PistaUV" && hit.collider.GetComponent<MeshRenderer>().material.name != "M_PistaUV (Instance)")
                {
                    //Debug.Log("no choca y es "+LastMeshRenderer);

                    if(LastMeshRenderer!=null)
                    {
                        LastMeshRenderer.material.SetFloat("_Aparicion", -light.intensity);
                        if (LastMeshRenderer.material.GetFloat("_Aparicion") <= 0)
                            LastMeshRenderer.material.SetFloat("_Aparicion", 0);
                    }

                    
                }
            
            }

            
            //Debug.DrawRay(transform.position, currentPointPositionRight + currentPointPositionUp, Color.green,light.range);
            //Debug.DrawRay(transform.position, currentPointPositionLeft + currentPointPositionUp, Color.green, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionRight + currentPointPositionDown, Color.green, light.range);
            //Debug.DrawRay(transform.position, currentPointPositionLeft + currentPointPositionDown, Color.green, light.range);


        }
        else if (LastMeshRenderer != null)
        {
            LastMeshRenderer.material.SetFloat("_Aparicion", -light.intensity);
            if (LastMeshRenderer.material.GetFloat("_Aparicion") <= 0)
                LastMeshRenderer.material.SetFloat("_Aparicion", 0);
        }

    }
    public void changeLight() //cambio de color de linterna
    {
        MeshRenderer meshRenderer;
            meshRenderer = GetComponentInParent<MeshRenderer>();
        Debug.Log(meshRenderer);
        if (OnOff == true)
        {  
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
        if (OnOff == true)
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

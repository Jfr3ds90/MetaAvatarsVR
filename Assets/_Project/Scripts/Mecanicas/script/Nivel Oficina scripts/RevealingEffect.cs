
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RevealingEffect : MonoBehaviour
{

    [SerializeField] private bool detectado;
    [SerializeField] private bool OnOff = false;
    [SerializeField] private AudioManager audioManager;

    [SerializeField] private MeshRenderer LastMeshRenderer;
    private Dictionary<string, int> AnimalsKnown = new Dictionary<string,int>();
    private Light light;
    private void Update()
    {
        if (OnOff== true)
        Appear();//tiene que volverse una corrutina
        /*
        if (Input.GetKeyUp(KeyCode.Q))
        {
            changeLight();
            OnOff = true;
        }
        
        if (Input.GetKeyUp(KeyCode.R)) 
            OnOffLight();
        */
    }
    private void Start()
    {
        light = GetComponent<Light>();
    }

    public void Appear()
    {
        if (OnOff == true)
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
                if (hit.collider.GetComponent<MeshRenderer>().material.name == "M_PistaUV" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_PistaUV (Instance)"||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_D" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_D (Instance)" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_A" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_A (Instance)" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_T" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_T (Instance)" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_O" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_O (Instance)" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_S" ||
                        hit.collider.GetComponent<MeshRenderer>().material.name == "M_Letra_S (Instance)"
                        )
                {
                        //hit.collider.GetComponent<MeshRenderer>().enabled = true;
                        Debug.Log("El Mesh renderer es "+ hit.collider.GetComponent<MeshRenderer>().name);
                        meshRenderer.material.SetFloat("_Aparicion",-light.intensity/*hit.distance*/);
                        meshRenderer.material.SetVector("_PosicionLuz", hit.point - hit.transform.position);// obtiene la posicion del rayo - posicion del objeto
                        detectado = true;
                        Debug.Log(hit.distance);// distancia de entre quien apunta y donde llega
                     if (meshRenderer.material.GetFloat("_Aparicion") >= 1)
                        meshRenderer.material.SetFloat("_Aparicion", 1);

                        string LastMaterialName = hit.collider.GetComponent<MeshRenderer>().material.name;
                        if (!AnimalsKnown.ContainsKey(LastMaterialName))
                        {
                            AnimalsKnown.Add(LastMaterialName, 1);
                            audioManager.FindAnimals = AnimalsKnown.Count;
                            audioManager.NarratorLinesActivation();
                            Debug.Log("paso");
                        }
                        else
                        {
                            Debug.Log("no paso");
                        }
                            LastMeshRenderer = meshRenderer;
                }

                    else
                    {
                        detectado = false;
                        if (LastMeshRenderer != null)
                        {
                            LastMeshRenderer.material.SetFloat("_Aparicion", -light.intensity);
                            if (LastMeshRenderer.material.GetFloat("_Aparicion") >= 1)
                                LastMeshRenderer.material.SetFloat("_Aparicion", 1);
                            Debug.Log(LastMeshRenderer + " Existe");
                        }
                    }
            }

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
                if (LastMeshRenderer != null)
                    LastMeshRenderer.material.SetFloat("_Aparicion", 1);
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
            if (LastMeshRenderer != null)
                LastMeshRenderer.material.SetFloat("_Aparicion", 1);
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

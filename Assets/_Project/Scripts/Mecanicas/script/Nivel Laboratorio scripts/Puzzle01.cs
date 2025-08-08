using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Puzzle01 : MonoBehaviour
{
    public GameObject[] laberynth;//scale 1.6 pos 0  |  0.8 dist / -2,3.625 inicial pos  / 0.75 tam | grilla(6,10)
    public GameObject liquid,pos,block,presentLaber,instrctions,spawnLiquid;
    public VictoryPuzzle vp;
    public int amount;
    float a;
   // public Dictionary<Vector2, GameObject> objectItem= new Dictionary<Vector2, GameObject>();
   List <GameObject> objectPos = new List<GameObject>(), liquidTotal = new List<GameObject>();
    void Update()
    {
       if (Input.GetKeyUp(KeyCode.V))
            Instantiate (liquid,pos.transform.position,pos.transform.rotation);

        if (Input.GetKeyUp(KeyCode.LeftArrow))
            LeftMovement();
        if (Input.GetKeyUp(KeyCode.DownArrow))
            DownMovement();
        if (Input.GetKeyUp(KeyCode.UpArrow))
            UpMovement();
        if (Input.GetKeyUp(KeyCode.RightArrow))
            RightMovement();
        if (Input.GetKeyUp(KeyCode.Z))
            GreenMovement();
    }
    private void Start()
    {
        // pos = presentLaber.GetComponentInChildren<SelectorID>().gameObject;//cambiar a cuando se cambie de laberinto
        vp=FindAnyObjectByType<VictoryPuzzle>();
    }
    private void LateUpdate()
    {
        if (instrctions.activeSelf == false)
        { 
            a += Time.deltaTime;
            if(a >= 0.5f)
            {
                GameObject l=Instantiate(liquid,spawnLiquid.transform);
                l.transform.SetParent(transform);
                liquidTotal.Add(l);
                a= 0;
            }
        }
    }
    public void LeftMovement()
    {
        
        if(instrctions.activeSelf==false)
        {   
            if (pos.transform.localPosition.x - 0.128f >= -0.523)
                pos.transform.localPosition -= new Vector3(0.128f, 0);
        }
        else if(instrctions.activeSelf == true)
            instrctions.SetActive(false);

    }
    public void DownMovement()
    {
        if (instrctions.activeSelf == false)
        {
            if (pos.transform.localPosition.y - 0.128f >= -0.7)
                pos.transform.localPosition -= new Vector3(0, 0.128f);
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void UpMovement()
    {
        if (instrctions.activeSelf == false)
        {
            if (pos.transform.localPosition.y + 0.128f <= 0.58)
                pos.transform.localPosition += new Vector3(0, 0.128f);
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void RightMovement()
    {
        if (instrctions.activeSelf == false)
        {
            if (pos.transform.localPosition.x + 0.128f <= 0.14f)
                pos.transform.localPosition += new Vector3(0.128f, 0);
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void GreenMovement()
    {
        if (instrctions.activeSelf == false)
        {
            if (objectPos.Count >= 6)
            {
                Destroy(objectPos[0]);
                objectPos.RemoveAt(0);
            }
            GameObject a = Instantiate(block, pos.transform);
            a.transform.SetParent(transform);
            objectPos.Add(a);    
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }

    public void EndLaberynth()
    {
        if(laberynth.Length>amount)
        {            
            for (int i = 0;i<liquidTotal.Count;i++)            
                Destroy(liquidTotal[i]);            

            Destroy(presentLaber);
            presentLaber=  Instantiate(laberynth[amount]);
            presentLaber.transform.SetParent(transform);
            presentLaber.transform.localPosition = new Vector3(-0.2030001f, 0,0) ;
            vp = FindAnyObjectByType<VictoryPuzzle>();

            liquidTotal.Clear();

        }
    }
}

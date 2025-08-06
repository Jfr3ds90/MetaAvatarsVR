using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Puzzle01 : MonoBehaviour
{
    public GameObject[] laberynth;//scale 1.6 pos 0  |  0.8 dist / -2,3.625 inicial pos  / 0.75 tam | grilla(6,10)
    public GameObject liquid,pos,block,presentLaber;
    Vector2 placeSelected;
    bool ObjectSelected;
   // public Dictionary<Vector2, GameObject> objectItem= new Dictionary<Vector2, GameObject>();
   List <GameObject> objectPos = new List<GameObject>();
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
    }
    public void LeftMovement()
    {
        var last = placeSelected;
        

      if(pos.transform.localPosition.x- 0.128f >= -0.523)
        pos.transform.localPosition -= new Vector3 (0.128f,0);

    }
    public void DownMovement()
    {
        var last = placeSelected;

        if (pos.transform.localPosition.y - 0.128f >= -0.7)
            pos.transform.localPosition -= new Vector3(0, 0.128f);

    }
    public void UpMovement()
    {
        var last = placeSelected;

        if (pos.transform.localPosition.y + 0.128f <= 0.58)
            pos.transform.localPosition += new Vector3(0, 0.128f);

    }
    public void RightMovement()
    {
        var last = placeSelected;

        if (pos.transform.localPosition.x + 0.128f <= 0.14f)
            pos.transform.localPosition += new Vector3(0.128f, 0);

    }
    public void GreenMovement()
    {
        if (objectPos.Count >= 6)
        {

            Destroy(objectPos[0]);
            objectPos.RemoveAt(0);
        }
        GameObject a;
        a = Instantiate(block, pos.transform);
        a.transform.SetParent(transform);
        objectPos.Add(a);   

       
    }

    public void EndLaberynth()
    {
        Debug.LogWarning("Termino el puzzle");
    }
}

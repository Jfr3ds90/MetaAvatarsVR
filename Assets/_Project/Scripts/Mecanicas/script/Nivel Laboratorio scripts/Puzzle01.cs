using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Puzzle01 : MonoBehaviour
{
    public GameObject[] laberynth;//scale 1.6 pos 0  |  0.8 dist / -2,3.625 inicial pos  / 0.75 tam | grilla(6,10)
    public GameObject liquid,pos,objectVertical,objectHorizontal,presentLaber;
    Vector2 placeSelected;
    bool ObjectSelected;
    public Dictionary<Vector2, GameObject> objectItem= new Dictionary<Vector2, GameObject>();
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
        pos = presentLaber.GetComponentInChildren<SelectorID>().gameObject;//cambiar a cuando se cambie de laberinto
    }
    public void LeftMovement()
    {
        var last = placeSelected;
        
       // if (objectItem.ContainsKey(new Vector2(placeSelected.x-1,placeSelected.y)))       
       //     placeSelected.x-=1;        
       // else       
      //      placeSelected = objectItem.Count;
      if(pos.transform.position.x-0.8f>=-2)
        pos.transform.position -=new Vector3 (0.8f,0);
       // GameObject lastObj; objectItem.TryGetValue(last,out lastObj);
       // Destroy(lastObj);
       // GameObject newObj; objectItem.TryGetValue(placeSelected, out newObj);
       // Instantiate(newObj);
    }
    public void DownMovement()
    {
        var last = placeSelected;
        // if (objectItem.ContainsKey(new Vector2(placeSelected.x,placeSelected.y-1)))       
        //    placeSelected.y-=1;
        //   else       
        //       placeSelected = objectItem.Count;
        if (pos.transform.position.y - 0.8f >= -3.625f)
            pos.transform.position -= new Vector3(0, 0.8f);
       // GameObject lastObj; objectItem.TryGetValue(last, out lastObj);
       // lastObj.SetActive(false);
       // GameObject newObj; objectItem.TryGetValue(placeSelected, out newObj);
       // newObj.SetActive(true);
    }
    public void UpMovement()
    {
        var last = placeSelected;
        //  if (objectItem.ContainsKey(new Vector2(placeSelected.x, placeSelected.y + 1)))
        //     placeSelected.y += 1;
        //    else       
        //       placeSelected = 0;
        if (pos.transform.position.y + 0.8f <=3.625f)
            pos.transform.position += new Vector3(0, 0.8f);
        // GameObject lastObj; objectItem.TryGetValue(last, out lastObj);
        // lastObj.SetActive(false);
        // GameObject newObj; objectItem.TryGetValue(placeSelected, out newObj);
        // newObj.SetActive(true);
    }
    public void RightMovement()
    {
        var last = placeSelected;
        // if (objectItem.ContainsKey(new Vector2(placeSelected.x + 1, placeSelected.y)))
        //     placeSelected.x += 1;
        //     else       
        //         placeSelected =0;
        if (pos.transform.position.x - 0.8f <= 2)
            pos.transform.position += new Vector3(0.8f, 0);
        // GameObject lastObj; objectItem.TryGetValue(last, out lastObj);
        // lastObj.SetActive(false);
        // GameObject newObj; objectItem.TryGetValue(placeSelected, out newObj);
        // newObj.SetActive(true);
    }
    public void GreenMovement()
    {
       // if (placeSelected != null)
        {
            GameObject Gselected; objectItem.TryGetValue(placeSelected,out Gselected);
            Gselected.GetComponent<SelectorID>().selected = true;
        }
    }
}

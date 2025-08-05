using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Puzzle01 : MonoBehaviour
{
    public Sprite[] laberynth;//scale 1.6 pos 0
    public GameObject liquid,pos,objectVertical,objectHorizontal;
    Vector2 placeSelected;
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

    public void LeftMovement()
    {
        if(objectItem.ContainsKey(new Vector2(placeSelected.x-1,placeSelected.y) ))
        {
            placeSelected.x-=1;
        }
    }
    public void DownMovement()
    {
        if (objectItem.ContainsKey(new Vector2(placeSelected.x, placeSelected.y-1)))
        {
            placeSelected.y-=1;
        }
    }
    public void UpMovement()
    {
        if (objectItem.ContainsKey(new Vector2(placeSelected.x, placeSelected.y+1)))
        {
            placeSelected.y+=1;
        }
    }
    public void RightMovement()
    {
        if (objectItem.ContainsKey(new Vector2(placeSelected.x + 1, placeSelected.y)))
        {
            placeSelected.x+=1;
        }
    }
    public void GreenMovement()
    {
        if (placeSelected != null)
        {
            GameObject Gselected; objectItem.TryGetValue(placeSelected,out Gselected);
            Gselected.GetComponent<SelectorID>().selected = true;
        }
    }
}

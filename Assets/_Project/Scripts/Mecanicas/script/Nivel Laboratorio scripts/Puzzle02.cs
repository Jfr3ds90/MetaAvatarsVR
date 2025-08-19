using System.Collections.Generic;
using UnityEngine;

public class Puzzle02 : MonoBehaviour
{
   public Sprite[] empty, full;
    public GameObject[] Empty,Full;
    public GameObject pos, instrctions;
    public Vector2 selectPos = new Vector2(0, 0);
    Dictionary<GameObject,Vector2> Coord = new Dictionary<GameObject, Vector2>();//segun el gameobject, es el mismo en la lista de int acorde al orden *tecnicamente*
    List<int> emptyPlaced = new List<int>();
    public int posObj;
    //0.225
    private void Awake()
    {
        for (int i = 0; i < Empty.Length; i++)
        {
            int value = Random.Range(0, empty.Length);
            Coord.Add(Empty[i], new Vector2(0, 0));
            Empty[i].GetComponent<SpriteRenderer>().sprite = empty[value];
            Full[i].GetComponent<SpriteRenderer>().sprite = full[value];
            emptyPlaced.Add(value);
          //  Empty[i].SetActive(true);
        }
    }
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.LeftArrow)) LMove();
        if (Input.GetKeyUp(KeyCode.RightArrow)) RMove();
        if (Input.GetKeyUp(KeyCode.UpArrow)) UMove();
        if (Input.GetKeyUp(KeyCode.DownArrow)) DMove();
        if (Input.GetKeyUp(KeyCode.Z)) GButton(); 
    }
    public void LMove()
    {
        if (instrctions.activeSelf == false)
        {
            if(selectPos.x>0)
            {
                pos.transform.localPosition -= new Vector3(0.225f,0);
                selectPos.x -= 1;
                posObj -= 1;
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void RMove()
    {
        if (instrctions.activeSelf == false)
        {
            if (selectPos.x  < 5)
            {
                pos.transform.localPosition += new Vector3(0.225f, 0);
                selectPos.x += 1;
                posObj += 1;
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void UMove()
    {
        if (instrctions.activeSelf == false)
        {
            if (selectPos.y < 0)
            {
                pos.transform.localPosition += new Vector3(0, 0.225f);
                selectPos.y += 1;
                posObj -= 6;
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void DMove()
    {
        if (instrctions.activeSelf == false)
        {
            if (selectPos.y > -5)
            {
                pos.transform.localPosition -= new Vector3(0, 0.225f);
                selectPos.y -= 1;
                posObj += 6;
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void GButton()
    {
       Debug.LogWarning("El sprite utilizado es "+emptyPlaced.IndexOf(posObj));
    }
}

using System.Collections.Generic;
using UnityEngine;

public class Puzzle02 : MonoBehaviour
{
   public Sprite[] empty, full;
    public GameObject[] Empty,Full;
    public GameObject pos, instrctions;
    public Vector2 selectPos = new Vector2(0, 0);
    Dictionary<GameObject,Vector2> Coord = new Dictionary<GameObject, Vector2>();
    //0.225
    private void Awake()
    {
        for (int i = 0; i < Empty.Length; i++)
        Coord.Add(Empty[i],new Vector2(0,0));
    }
    public void LMove()
    {
        if (instrctions.activeSelf == false)
        {
            pos.transform.localPosition -= new Vector3(0.225f,0);
            selectPos.x -= 1;
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void RMove()
    {
        if (instrctions.activeSelf == false)
        {
            pos.transform.localPosition += new Vector3(0.225f, 0);
            selectPos.x += 1;
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void UMove()
    {
        if (instrctions.activeSelf == false)
        {
            pos.transform.localPosition += new Vector3(0, 0.225f);
            selectPos.y += 1;
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void DMove()
    {
        if (instrctions.activeSelf == false)
        {
            pos.transform.localPosition -= new Vector3(0, 0.225f);
            selectPos.y -= 1;
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
}

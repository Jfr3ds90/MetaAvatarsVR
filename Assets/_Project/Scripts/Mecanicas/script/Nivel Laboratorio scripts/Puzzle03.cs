using System.Collections.Generic;
using UnityEngine;

public class Puzzle03 : MonoBehaviour
{
    public Sprite[] level1, level2, level3, level4, level5, level6, level7, level8, level9;
    public GameObject[] squarePos;
    public GameObject pos, instrctions;
    int currentLevel = 0;
    bool trueImage = true;
    List<Sprite> LSprite= new List<Sprite>();
    List<int> lis = new List<int>();
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.LeftArrow)) LMove();
        if (Input.GetKeyUp(KeyCode.RightArrow)) RMove();
        if (Input.GetKeyUp(KeyCode.UpArrow)) UMove();
        if (Input.GetKeyUp(KeyCode.DownArrow)) DMove();
    }
    public void LMove()
    {
        if (instrctions.activeSelf == false)
        {
            if(trueImage == true)
           {RandomSprite(); trueImage = false; }
            else if(trueImage == false && pos.transform.localPosition.x- 0.325f > -0.815f)
            {
                pos.transform.localPosition -= new Vector3(0.325f,0);
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void RMove()
    {
        if (instrctions.activeSelf == false)
        {
            if (trueImage == true)
                { RandomSprite(); trueImage = false; }
            else if (trueImage == false && pos.transform.localPosition.x+ 0.325f < 0.485f)
            {
                pos.transform.localPosition += new Vector3(0.325f, 0);
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void UMove()
    {
        if (instrctions.activeSelf == false )
        {
            if (trueImage == true)
               { RandomSprite(); trueImage = false; }
            else if (trueImage == false && pos.transform.localPosition.y+0.325f < 0.5f)
            {
                pos.transform.localPosition += new Vector3(0, 0.325f);
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void DMove()
    {
        if (instrctions.activeSelf == false)
        {
            if (trueImage == true)
               { RandomSprite(); trueImage = false; }
            else if (trueImage == false && pos.transform.localPosition.y-0.325f > -0.5f) 
            {
                pos.transform.localPosition -= new Vector3(0, 0.325f);
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void GButton()
    {
        if (instrctions.activeSelf == false)
        {
            if (trueImage == true)
                { RandomSprite(); trueImage = false; }
            else if (trueImage == false)
            { }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void EndLevel()
    {
        currentLevel++;
        for (int i = 0; i < squarePos.Length; i++) 
        {
            switch (currentLevel)
            {
                case 1:squarePos[i].GetComponent<SpriteRenderer>().sprite = level2[i];
                    break;
                case 2:squarePos[i].GetComponent<SpriteRenderer>().sprite = level3[i];
                    break;
                case 3:squarePos[i].GetComponent<SpriteRenderer>().sprite = level4[i];
                    break;
                case 4:squarePos[i].GetComponent<SpriteRenderer>().sprite = level5[i];
                    break;
                case 5:squarePos[i].GetComponent<SpriteRenderer>().sprite = level6[i];
                    break;
                case 6:squarePos[i].GetComponent<SpriteRenderer>().sprite = level7[i];
                    break;
                case 7:squarePos[i].GetComponent<SpriteRenderer>().sprite = level8[i];
                    break;
                case 8:squarePos[i].GetComponent<SpriteRenderer>().sprite = level9[i];
                    break;
            }

        }
    }
    void RandomSprite()
    {
       
        switch(currentLevel)
        {
            case 0:
                foreach (Sprite s in level1)
                    LSprite.Add(s); Debug.LogWarning(LSprite[0]+ " es el sprite 0");
               LSprite.RemoveAt( Random.Range(0,LSprite.Count));//el vacio
                for (int i = 0; i< squarePos.Length;i++)
                {
                        RS(i);                    
                }
                lis.Clear();
                break;
            case 1:
                foreach (Sprite s in level2)
                    LSprite.Add(s);
                break;
            case 2:
                foreach (Sprite s in level3)
                    LSprite.Add(s);
                break;
            case 3:
                foreach (Sprite s in level4)
                    LSprite.Add(s);
                break;
            case 4:
                foreach (Sprite s in level5)
                    LSprite.Add(s);
                break;
            case 5:
                foreach (Sprite s in level6)
                    LSprite.Add(s);
                break;
            case 6:
                foreach (Sprite s in level7)
                    LSprite.Add(s);
                break;
            case 7:
                foreach (Sprite s in level8)
                    LSprite.Add(s);
                break;
            case 8:
                foreach (Sprite s in level1)
                    LSprite.Add(s);
                break;
        }
        LSprite.Clear();
    }
    void RS(int i)
    {
        int v = Random.Range(0, LSprite.Count);
        if (!lis.Contains(v))
        {
            lis.Add(v);
            squarePos[i].GetComponent<SpriteRenderer>().sprite = level1[v];//solo llega al 8
            LSprite.RemoveAt(v);
        }
        else if (lis.Contains(v))
            RS(i);
    }
}

using ExitGames.Client.Photon.StructWrapping;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Puzzle03 : MonoBehaviour
{
    public Sprite[] level1, level2, level3, level4, level5, level6, level7, level8, level9;
    public GameObject[] squarePos;
    public GameObject pos, instrctions;
    int currentLevel = 0;
    public int empty;
    bool trueImage = true;
    List<Sprite> LSprite= new List<Sprite>();
    List<int> lis = new List<int>();
   public Vector2 selectPos = new Vector2(0, 0),emptyPos;//maximo 3,-3
    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.LeftArrow)) LMove();
        if (Input.GetKeyUp(KeyCode.RightArrow)) RMove();
        if (Input.GetKeyUp(KeyCode.UpArrow)) UMove();
        if (Input.GetKeyUp(KeyCode.DownArrow)) DMove();
        if (Input.GetKeyUp(KeyCode.Z)) GButton();
        if (Input.GetKeyUp(KeyCode.R)) DebugResolution();
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
                selectPos.x -= 1;
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
                selectPos.x += 1;
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
                selectPos.y += 1;
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
                selectPos.y -= 1;
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
            else if (trueImage == false)//y = empty / -4;x = empty % 4;
            {
                if (selectPos.x-1 == emptyPos.x && selectPos.y == emptyPos.y) 
                {
                    if(CheckFull()==true)
                        EndLevel();
                    else
                    {
                        squarePos[empty].GetComponent<SpriteRenderer>().sprite = squarePos[empty+1].GetComponent<SpriteRenderer>().sprite;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = Color.white;
                        emptyPos = selectPos;
                        empty += 1;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 255);
                    }      
                }
                else if (selectPos.x+1 == emptyPos.x && selectPos.y == emptyPos.y)
                {
                    if (CheckFull() == true)
                        EndLevel();
                    else
                    {
                        squarePos[empty].GetComponent<SpriteRenderer>().sprite = squarePos[empty - 1].GetComponent<SpriteRenderer>().sprite;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = Color.white;
                        emptyPos = selectPos;
                        empty -= 1;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 255);
                    }
                }
                else if (selectPos.y-1 == emptyPos.y && selectPos.x == emptyPos.x)
                {
                    if (CheckFull() == true)
                        EndLevel();
                    else
                    {
                        squarePos[empty].GetComponent<SpriteRenderer>().sprite = squarePos[empty - 4].GetComponent<SpriteRenderer>().sprite;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = Color.white;
                        emptyPos = selectPos;
                        empty -= 4;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 255);
                    }
                }
                else if (selectPos.y+1 == emptyPos.y && selectPos.x == emptyPos.x)
                {
                    if (CheckFull() == true)
                        EndLevel();
                    else
                    { 
                        squarePos[empty].GetComponent<SpriteRenderer>().sprite = squarePos[empty + 4].GetComponent<SpriteRenderer>().sprite;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = Color.white;
                        emptyPos = selectPos;
                        empty += 4;
                        squarePos[empty].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 255);
                    }
                }
            }
        }
        else if (instrctions.activeSelf == true)
            instrctions.SetActive(false);
    }
    public void EndLevel()
    {
        currentLevel++;
        for (int i = 0; i < squarePos.Length; i++)   
            ChangeSprite(i, i);
        LSprite.Clear();
        trueImage = true;
    }
    void ChangeSprite(int a, int b)
    {
        switch (currentLevel)
        {
            case 0:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level1[b];
                break;
            case 1:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level2[b];
                break;
            case 2:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level3[b];
                break;
            case 3:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level4[b];
                break;
            case 4:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level5[b];
                break;
            case 5:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level6[b];
                break;
            case 6:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level7[b];
                break;
            case 7:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level8[b];
                break;
            case 8:
                squarePos[a].GetComponent<SpriteRenderer>().sprite = level9[b];
                break;
        }
    }
    void RandomSprite()
    {
        int x, y;
        switch(currentLevel)
        {
            case 0:
                foreach (Sprite s in level1)
                    LSprite.Add(s);
                for (int i = 0; i< squarePos.Length;i++)               
                        RS(i);                                                    
                break;
            case 1:
                foreach (Sprite s in level2)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);               
                break;
            case 2:
                foreach (Sprite s in level3)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);               
                break;
            case 3:
                foreach (Sprite s in level4)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);               
                break;
            case 4:
                foreach (Sprite s in level5)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);              
                break;
            case 5:
                foreach (Sprite s in level6)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);               
                break;
            case 6:
                foreach (Sprite s in level7)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);                
                break;
            case 7:
                foreach (Sprite s in level8)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);              
                break;
            case 8:
                foreach (Sprite s in level9)
                    LSprite.Add(s);
                for (int i = 0; i < squarePos.Length; i++)
                    RS(i);                
                break;
        }
        lis.Clear();
        empty = Random.Range(0, squarePos.Length);
        squarePos[empty].GetComponent<SpriteRenderer>().color = new Color(0, 0, 0, 255);
        y = empty / -4;
        x = empty % 4;
        emptyPos = new Vector2(x, y);
    }
    void RS(int i)
    {
        int v = Random.Range(0, LSprite.Count);
        if (!lis.Contains(v))
        {
            lis.Add(v);
            ChangeSprite(i,v);
        }
        else if (lis.Contains(v))
            RS(i);   
    }
    bool CheckFull()
    {
        int v = 0;

        for (int i = 0; i < squarePos.Length; i++)
        
            if (LSprite[i] == squarePos[i].GetComponent<SpriteRenderer>().sprite)
               { v++; Debug.LogWarning(i + " es el correcto "+ LSprite[i]); }
            //Debug.LogWarning(v + " es la cantidad en posicion correcta ");
        

        if (v==16) {squarePos[empty].GetComponent<SpriteRenderer>().color= Color.white; return true;}
        else
        return false;
    }
    void DebugResolution()
    {
        for (int i = 0; i < squarePos.Length; i++)
           squarePos[i].GetComponent<SpriteRenderer>().sprite = LSprite[i];                
    }
}

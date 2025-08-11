using UnityEngine;

public class Puzzle03 : MonoBehaviour
{
    public Sprite[] level1, level2, level3, level4, level5, level6, level7, level8, level9;
    public GameObject[] squarePos;
    public GameObject pos, instrctions;
    int currentLevel = 0;
    bool trueImage = true;
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
            trueImage = false;
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
                trueImage = false;
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
                trueImage = false;
            else if (trueImage == false && pos.transform.localPosition.y+0.325f < 0.475f)
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
                trueImage = false;
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
                trueImage = false;
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
                case 2:squarePos[i].GetComponent<SpriteRenderer>().sprite = level2[i];
                    break;
                case 3:squarePos[i].GetComponent<SpriteRenderer>().sprite = level3[i];
                    break;
                case 4:squarePos[i].GetComponent<SpriteRenderer>().sprite = level4[i];
                    break;
                case 5:squarePos[i].GetComponent<SpriteRenderer>().sprite = level5[i];
                    break;
                case 6:squarePos[i].GetComponent<SpriteRenderer>().sprite = level6[i];
                    break;
                case 7:squarePos[i].GetComponent<SpriteRenderer>().sprite = level7[i];
                    break;
                case 8:squarePos[i].GetComponent<SpriteRenderer>().sprite = level8[i];
                    break;
                case 9:squarePos[i].GetComponent<SpriteRenderer>().sprite = level9[i];
                    break;
            }

        }
    }
}

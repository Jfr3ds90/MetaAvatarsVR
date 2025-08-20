using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Puzzle02 : MonoBehaviour
{
   public Sprite[] empty, full;
    public GameObject[] Empty,Full;
    public GameObject pos, instrctions;
    public Vector2 selectPos = new Vector2(0, 0);
    Dictionary<GameObject,Vector2> Coord = new Dictionary<GameObject, Vector2>();//segun el gameobject, es el mismo en la lista de int acorde al orden *tecnicamente*
    Dictionary<GameObject, int> Rotable = new Dictionary<GameObject, int>();//para recordar cuanto fue rotado
    public int posObj;
    //0.225
    private void Awake()
    {
        for (int i = 0; i < Empty.Length; i++)
        {
            int value = Random.Range(0, empty.Length);
            Coord.Add(Empty[i], new Vector2(0, 0));
            Empty[i].GetComponent<Image>().sprite = empty[value];
            Full[i].GetComponent<Image>().sprite = full[value];
            
            if(value == 1|| value == 3)
            {
                int rot = Random.Range(0, 4);
                switch (rot)
                {
                    case 0:
                        Empty[i].transform.rotation = Quaternion.Euler(0, 0, 0);
                        Full[i].transform.rotation = Quaternion.Euler(0, 0, 0);
                        break;
                    case 1:
                        Empty[i].transform.rotation = Quaternion.Euler(0, 0, 90);
                        Full[i].transform.rotation = Quaternion.Euler(0, 0, 90);
                        break;
                    case 2:
                        Empty[i].transform.rotation = Quaternion.Euler(0, 0, 180);
                        Full[i].transform.rotation = Quaternion.Euler(0, 0, 180);
                        break;
                    case 3:
                        Empty[i].transform.rotation = Quaternion.Euler(0, 0, 270);
                        Full[i].transform.rotation = Quaternion.Euler(0, 0, 270);
                        break;
                }
                Rotable.Add(Empty[i],rot);
            }
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
       //Debug.LogWarning("El sprite utilizado es "+Coord.ElementAt(posObj).Key.GetComponent<Image>().sprite);
        string sprite = Coord.ElementAt(posObj).Key.GetComponent<Image>().sprite.name;
        switch (sprite)
        {
            case "Puzzle02_Vacio_Cuadruple":
                Debug.LogWarning("opcion 1");
                break;
            case "Puzzle02_Vacio_Triple":
                Debug.LogWarning("opcion 2");
                break;
            case "Puzzle02_Vacio_Vertical":
                Debug.LogWarning("opcion 3");
                break;
            case "Puzzle02_Vacio_Horizontal":
                Debug.LogWarning("opcion 4");
                break;
            case "Puzzle02_Vacio_Desviación":
                Debug.LogWarning("opcion 5");
                break;
               
        }
    }
}

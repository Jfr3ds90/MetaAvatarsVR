using System;
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
            int value;
            if (i!=0)
            value = UnityEngine.Random.Range(0, empty.Length);
            else
                value = 0;
            Coord.Add(Empty[i], new Vector2(0, 0));
            Empty[i].GetComponent<Image>().sprite = empty[value];
            Full[i].GetComponent<Image>().sprite = full[value];
             Image im =Full[i].GetComponent<Image>();
            if(value == 1|| value == 3)
            {
                int rot = UnityEngine.Random.Range(0, 4);
                simpleRotate(rot,i);
                Rotable.Add(Empty[i],rot);
            }
            switch(value)
            {
                case 0://cuadruple Analizar bien desde donde tiene que venir
                    im.fillMethod = Image.FillMethod.Horizontal;//Radial360 no es recomendado, Variantes: horizontal | Vertical
                    im.fillOrigin = (int)Image.OriginHorizontal.Left; //Variante horizontal: Left _ Right | vertical: Bottom _ Top 
                    break;
                case 1://desviacion
                    im.fillMethod = Image.FillMethod.Radial90;
                    im.fillOrigin = (int)Image.Origin90.TopLeft;//Radial90 TopLeft Variante: Clockwise
                    im.fillClockwise = true;
                    break;
                case 2://horizontal
                    im.fillMethod = Image.FillMethod.Horizontal;
                    im.fillOrigin = (int)Image.OriginHorizontal.Left;//variantes: Left | Right
                    break;
                case 3://triple
                    im.fillMethod = Image.FillMethod.Horizontal;//Radial180 no recomendado, Variantes: Vertical | Horizontal
                    im.fillOrigin = (int)Image.OriginHorizontal.Left;//Variantes: Vertical->Top | Horizontal: Left _ Right
                    break;
                case 4://vertical
                    im.fillMethod = Image.FillMethod.Vertical; 
                    im.fillOrigin = (int)Image.OriginVertical.Bottom;//variantes: Bottom | Top
                    break;
            }
           
            if (i == 0)
                Full[i].GetComponent<Image>().fillAmount = 1;          
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
        GameObject piece = Coord.ElementAt(posObj).Key;
        switch (sprite)
        {
            case "Puzzle02_Vacio_Cuadruple":
               // Debug.LogWarning("opcion 1");
                break;
            case "Puzzle02_Vacio_Triple":
                Rotable.TryGetValue(piece,out int valT);
                valT++;
                if (valT > 3)
                    valT = 0;
                int T = Array.IndexOf(Coord.Keys.ToArray(), piece);
                simpleRotate(valT, T);
                Rotable[piece] = valT;
                originFill(Coord.ElementAt(posObj).Key.GetComponent<Image>(), originDirection(posObj), 3, valT);
             //   Debug.LogWarning("opcion 2 con rotacion tipo: "+valT);
                break;
            case "Puzzle02_Vacio_Vertical":
                //es el 3 pasa al 1
            //    Debug.LogWarning("opcion 3");
                break;
            case "Puzzle02_Vacio_Horizontal":
                //es el 1 pasa al 3
            //    Debug.LogWarning("opcion 4");
                break;
            case "Puzzle02_Vacio_Desviación":
                Rotable.TryGetValue(piece, out int valD);
                valD++;
                if (valD > 3)
                    valD = 0;
              int D = Array.IndexOf(Coord.Keys.ToArray(), piece);
                simpleRotate(valD, D);
                Rotable[piece] = valD;
                originFill(Coord.ElementAt(posObj).Key.GetComponent<Image>(), originDirection(posObj), 1, valD);
                //  Debug.LogWarning("opcion con rotacion tipo: " + valD);
                break;
               
        }

       // Full[posObj].GetComponent<Image>().fillAmount = 1;//borrar una vez todo el sistema funcione
    }
    void originFill(Image im,int direction,int spr,int rot)//de donde viene el liquido
    {
        switch (direction)
        {
            case 0://izquierda
                switch(spr)
                {
                    case 0://cuadruple
                        im.fillMethod = Image.FillMethod.Horizontal;
                        im.fillOrigin = (int)Image.OriginHorizontal.Left;
                        break;
                    case 1://desviacion
                        switch (rot)//Radial90 TopLeft Variante: Clockwise
                        {
                            case 0:
                                im.fillAmount = 0;
                                break;
                            case 1:
                                im.fillAmount = 0;
                                break;
                            case 2:
                                im.fillClockwise = false;
                                break;
                            case 3:
                                im.fillClockwise = true;
                                break;
                        }
                        break;
                    case 2://horizontal
                        im.fillMethod = Image.FillMethod.Horizontal;
                        im.fillOrigin = (int)Image.OriginHorizontal.Left;
                        break;
                    case 3://triple
                        switch (rot)
                        {
                            case 0:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Right;
                                break;
                            case 1:
                                im.fillAmount = 0;
                                break;
                            case 2:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Left;
                                break;
                            case 3:
                                im.fillMethod = Image.FillMethod.Vertical;
                                im.fillOrigin = (int)Image.OriginVertical.Top;
                                break;
                        }
                        break;
                    case 4://vertical
                        im.fillAmount = 0;
                        break;

                }
                break;
            case 1://arriba
                switch (spr)
                {
                    case 0://cuadruple
                        im.fillMethod = Image.FillMethod.Vertical;
                        im.fillOrigin = (int)Image.OriginVertical.Top;
                        break;
                    case 1://desviacion
                        switch (rot)//Radial90 TopLeft Variante: Clockwise
                        {
                            case 0:
                                im.fillClockwise = true;
                                break;
                            case 1:
                                im.fillAmount= 0;
                                break;
                            case 2:
                                im.fillAmount = 0;
                                break;
                            case 3:
                                im.fillClockwise = false;
                                break;
                        }
                        break;
                    case 2://horizontal
                        im.fillAmount = 0;
                        break;
                    case 3://triple
                        switch (rot)
                        {
                            case 0:
                                im.fillMethod = Image.FillMethod.Vertical;
                                im.fillOrigin = (int)Image.OriginVertical.Top;
                                break;
                            case 1:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Right;
                                break;
                            case 2:
                                im.fillAmount = 0;
                                break;
                            case 3:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Left;
                                break;
                        }
                        break;
                    case 4://vertical
                        im.fillMethod = Image.FillMethod.Vertical;
                        im.fillOrigin = (int)Image.OriginVertical.Top;
                        break;

                }
                break;
            case 2://derecha
                switch (spr)
                {
                    case 0://cuadruple
                        im.fillMethod = Image.FillMethod.Horizontal;
                        im.fillOrigin = (int)Image.OriginHorizontal.Right;
                        break;
                    case 1://desviacion
                        switch (rot)//Radial90 TopLeft Variante: Clockwise
                        {
                            case 0:
                                im.fillClockwise = false;
                                break;
                            case 1:
                                im.fillClockwise = true;
                                break;
                            case 2:
                                im.fillAmount = 0;
                                break;
                            case 3:
                                im.fillAmount = 0;
                                break;
                        }
                        break;
                    case 2://horizontal
                        im.fillMethod = Image.FillMethod.Horizontal;
                        im.fillOrigin = (int)Image.OriginHorizontal.Right;
                        break;
                    case 3://triple
                        switch (rot)
                        {
                            case 0:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Left;
                                break;
                            case 1:
                                im.fillMethod = Image.FillMethod.Vertical;
                                im.fillOrigin = (int)Image.OriginVertical.Top;
                                break;
                            case 2:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Right;
                                break;
                            case 3:
                                im.fillAmount = 0;
                                break;
                        }
                        break;
                    case 4://vertical
                        im.fillAmount = 0;
                        break;

                }
                break;
            case 3://abajo
                switch (spr)
                {
                    case 0://cuadruple
                        im.fillMethod = Image.FillMethod.Vertical;
                        im.fillOrigin = (int)Image.OriginVertical.Bottom;
                        break;
                    case 1://desviacion
                        switch (rot)
                        {
                            case 0:
                                im.fillAmount = 0;
                                break;
                            case 1:
                                im.fillClockwise = false;
                                break;
                            case 2:
                                im.fillClockwise = true;
                                break;
                            case 3:
                                im.fillAmount = 0;
                                break;
                        }
                        break;
                    case 2://horizontal
                        im.fillAmount = 0;
                        break;
                    case 3://triple
                        switch (rot)
                        {
                            case 0:
                                im.fillAmount = 0;
                                break;
                            case 1:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Left;
                                break;
                            case 2:
                                im.fillMethod = Image.FillMethod.Vertical;
                                im.fillOrigin = (int)Image.OriginVertical.Top;
                                break;
                            case 3:
                                im.fillMethod = Image.FillMethod.Horizontal;
                                im.fillOrigin = (int)Image.OriginHorizontal.Right;
                                break;
                        }
                        break;
                    case 4://vertical
                        im.fillMethod = Image.FillMethod.Vertical;
                        im.fillOrigin = (int)Image.OriginVertical.Bottom;
                        break;
                }
                break;
            case 4://2 conexiones o más
                im.fillAmount = 1;
                break;
            case -1://0 conexiones
                im.fillAmount = 0;
                break;
        }
    }
    void simpleRotate(int rot,int i)
    {
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
    }
    int originDirection(int selected)
    {
        int value = -1;bool connected = false;
        if (Coord.ElementAt(selected - 1).Key.GetComponent<Image>().fillAmount == 1)
        {
            if (connected == true)
                value = 4;
            else if(connected == false)
               { value = 0; connected = true;    }          
        }
        if(Coord.ElementAt(selected + 6).Key.GetComponent<Image>().fillAmount == 1)
            if (connected == true)
                value = 4;
            else if (connected == false)
            { value = 1; connected = true; }
        if (Coord.ElementAt(selected + 1).Key.GetComponent<Image>().fillAmount == 1)
            if (connected == true)
                value = 4;
            else if (connected == false)
            { value = 2; connected = true; }
        if (Coord.ElementAt(selected - 6).Key.GetComponent<Image>().fillAmount == 1)
            if (connected == true)
                value = 4;
            else if (connected == false)
            { value = 3; connected = true; }
        if (connected==false) value = -1;

                    return value;
    }
}

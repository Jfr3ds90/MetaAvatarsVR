using System.Collections.Generic;
using UnityEngine;

public class MainFigure : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<Vector3> sizeExample,realFigure;
    Dictionary<Vector3, bool> posTimeCube = new Dictionary<Vector3, bool>();
    [SerializeField] GameObject safe;
    //public Dictionary<Vector3,int> 
    //hacer una lista de los valores correctos
    //checkear que esos valores sean iguales a los del checkR
    private void Awake()
    {
        sizeExample.Clear();
    }
    public void AddCoord(int x,int y,int z,int scanned)
    {
        if (!sizeExample.Contains(new Vector3(x, y, z)))
        {
            switch (scanned)
            {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
                case 5:
                    break;
                case 6:
                    break;
            }
            sizeExample.Add(new Vector3(x, y, z));

            Debug.Log("las coordenadas ("+x+" "+y+" "+z+") han sido llamada "+scanned+" veces");
        }
        //else if (sizeExample.Contains(new Vector3(x, y, z)))
        //{
        //    sizeExample.Remove(new Vector3(x,y,z));
        //}

          //  Debug.Log(sizeExample.Count + " es la cantidad");
        for (int i = 0; i < sizeExample.Count; i++) //reviza si la coordenada existe
        {
            //Debug.Log(sizeExample[i].ToString() + " esta en la lista");

            //Debug.Log("esta en: " + i);
        }
    }
    /*public void CheckResult()
    {
        int completed = 0;
        for (int i = 0; i < realFigure.Count; i++)
        {
            if (sizeExample.Contains(realFigure[i]))
                completed++;
        }
        if (completed == realFigure.Count)
            Debug.Log("FIGURA HECHA");

    }*/
    public void CheckR( )
    {
        for (int i = 0; i < realFigure.Count; i++) 
        {
            for (int j = 0; j < sizeExample.Count; j++) 
            {
                if (realFigure[i] == sizeExample[i])
                {
                    Debug.Log("Esta hecho el " + sizeExample[i]);
                    /*
                    switch (scanned)
                    {
                        case 0:
                            break;
                        case 1:
                            break;
                        case 2:
                            break;
                        case 3:
                            break;
                        case 4:
                            break;
                        case 5:
                            break;
                        case 6:
                            break;
                    }
                    */
                    if (!posTimeCube.ContainsKey(sizeExample[i]))
                    posTimeCube.Add(sizeExample[i],true);
                }
            }
        }

        if (posTimeCube.Count == realFigure.Count)
        {
            safe.GetComponent<Switch>().OpenDoorAct();
            Debug.Log("Cantidad igual"); 
        }

    }
    public void fastSolution()
    {
        safe.GetComponent<Switch>().OpenDoorAct();
    }
}

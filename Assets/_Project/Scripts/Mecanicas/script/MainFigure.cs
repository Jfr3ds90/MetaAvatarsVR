using System.Collections.Generic;
using UnityEngine;

public class MainFigure : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<Vector3> sizeExample,realFigure;
    //hacer una lista de los valores correctos
    //checkear que esos valores sean iguales a los del checkR
    private void Awake()
    {
        sizeExample.Clear();
    }
    public void AddCoord(int x,int y,int z)
    {
        if (!sizeExample.Contains(new Vector3(x, y, z)))
            sizeExample.Add(new Vector3(x, y, z));

        Debug.Log(sizeExample.Count+" es la cantidad");
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
    public void CheckR(int x,int y,int z)
    {
        Debug.Log("La pieza esta en ("+x+","+y + "," + z +")");
    }
}

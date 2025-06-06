
using System.Collections.Generic;
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectTotals,posObject;
    private int objectsTotal; 
    public Transform tablePos;
    public Vector2 TotalArea;
    public GameObject[,] squares;
    public Vector2[] Value;
    public Material[] Arte;
    public GameObject CuadroPos;

    List<int> objectsUsed = new List<int>();//lista de objetos ya usados
    List<int> PosUsed = new List<int>();//lista de objetos ya usados
    private void OnEnable()
    {
        randomInPlaces(posObject,objectTotals);
    }
    void sorterOfPositions( int i, int j, GameObject[] objects, int v)
    {
        int boolean = Random.Range(0, 2); 
        for (int k = 0; k < Value.Length; k++)     
           if (Value[k].x == i && Value[k].y == j)           
                boolean = 0;
     
        switch (boolean)
        {
            case 0:
                Debug.Log("los valores "+i+","+j+" son vacios");
                break;
            case 1:
                if(tablePos!=null)
                squares[i, j] = Instantiate(objects[v], tablePos.position + new Vector3(i, 0, j), Quaternion.identity);
                break;
        }
    }
    void randomInSquare()
    {
        objectsTotal = objectTotals.Length;
        for (int i = 0; i < Mathf.RoundToInt(TotalArea.x); i++)      
            for (int j = 0; j < Mathf.RoundToInt(TotalArea.y); j++)
            {
                int randomObject = Random.Range(0, objectsTotal);
                sorterOfPositions(i, j, objectTotals, randomObject);
            }   
    }
    void randomInPlaces(GameObject[] pos, GameObject[] objects)//se instancia de manera aleatoria solo haciendolo 1 vez, solo falta hacer que siempre seand objetos diferentes
    {// se instancia objeto y se saca de la array
        //la posicion donde fue instanciado tambien se saca de la array
        //aleatoriedad de objetos disponibles y posiciones disponibles

        for (int i = 0; i < pos.Length; i++) //posicion aleatoria
        {
            int randomvalue = Random.Range(0,2);
            Debug.Log("spawneara "+randomvalue);
            if (PosUsed.Contains(i))
                randomvalue = 0;
            if(randomvalue==1)
            {
                for (int j = 0;j < objects.Length; j++)//objeto aleatorio
                {
                    randomObjects(pos, objects, i, j);
                }
            }
        }
        if (pos.Length != PosUsed.Count)
            randomInPlaces(pos, objects);
    }
    void randomObjects(GameObject[] pos, GameObject[] objects,int i,int j)
    {

        int value = Random.Range(0, objects.Length);
        // una vez salga el numero revisar que este no haya salido antes
        Debug.Log("aparecio " + objects[j] + " es " + objects[j].name + "(Clone)");
        if (!objectsUsed.Contains(value))// si no contiene el valor la lista, entonces se agrega
        {
            Instantiate(objects[value], pos[i].transform);
            objectsUsed.Add(value);
            PosUsed.Add(i);
        }
        else// si ya lo contiene, se hace de nuevo
        {
            randomObjects(pos, objects,i,j);
        }
    }
    void randomPos()
    {

    }
    void changeM()
    {
        int randomvalue = Random.Range(0, Arte.Length);
        CuadroPos.GetComponent<Renderer>().material = Arte[randomvalue];
    }
}

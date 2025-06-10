
using System.Collections.Generic;
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectTotals,posObject;
   // private int objectsTotal; 
   // public Transform tablePos;
   // public Vector2 TotalArea;
   // public GameObject[,] squares;
   // public Vector2[] Value;
    public Material[] Arte;
    public GameObject[] CuadroPos;

    List<int> objectsUsed = new List<int>();//lista de objetos ya usados
    List<int> PosUsed = new List<int>();//lista de objetos ya usados
    private void OnEnable()
    {   
        randomInPlaces(posObject,objectTotals); 
        
    }
    private void Start()
    {
        changeMaterial();
    }
    //private void Update()
    //{if(Input.GetKeyUp(KeyCode.V))
    //        changeMaterial();
    //}
    /* void sorterOfPositions( int i, int j, GameObject[] objects, int v)
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
     }*/
    /*void randomInSquare()
    {
        objectsTotal = objectTotals.Length;
        for (int i = 0; i < Mathf.RoundToInt(TotalArea.x); i++)      
            for (int j = 0; j < Mathf.RoundToInt(TotalArea.y); j++)
            {
                int randomObject = Random.Range(0, objectsTotal);
                sorterOfPositions(i, j, objectTotals, randomObject);
            }   
    }*/
    void randomInPlaces(GameObject[] pos, GameObject[] objects)//se instancia de manera aleatoria solo haciendolo 1 vez, solo falta hacer que siempre seand objetos diferentes
    {// se instancia objeto y se saca de la array
        //la posicion donde fue instanciado tambien se saca de la array
        //aleatoriedad de objetos disponibles y posiciones disponibles

        for (int i = 0; i <= pos.Length; i++) //posicion aleatoria
        {
            int randomvalue = Random.Range(0,2);
            //Debug.Log("spawneara "+randomvalue);
            switch(randomvalue)
            {
                case 0:
                    break;
                case 1:
                    if (!PosUsed.Contains(i))
                    {
                        //for (int j = 0; j < objects.Length; j++)                   
                            randomObjects(pos, objects, i);//objeto aleatorio
                        PosUsed.Add(i);//Debug.Log("Fue agregado " + i);
                        break;
                    }
                    else if (PosUsed.Contains(i))
                        { i++;
                        //Debug.Log("Ya estaba " + i);
                        break;};                        
                    break;
            }
            //Debug.Log(i+" esta en la lista? "+PosUsed.Contains(i));
        }
        if (objects.Length != PosUsed.Count)
            randomInPlaces(pos, objects);
   
        //Debug.Log(PosUsed);

        //int value = Random.Range(0, pos.Length);

        //// una vez salga el numero revisar que este no haya salido antes
        //if (!objectsUsed.Contains(value))// si no contiene el valor la lista, entonces se agrega
        //{
        //    objectsUsed.Add(value);
        //    PosUsed.Add(i);
        //    Instantiate(objects[value], pos[i].transform);

        //}
        //else if (objectsUsed.Contains(value))// si ya lo contiene, se hace de nuevo
        //{
        //    if (PosUsed.Contains(i))
        //    { randomInPlaces(pos, objects); }
        //    else
        //        randomObjects(pos, objects, i, j);
        //}
    }
    void randomObjects(GameObject[] pos, GameObject[] objects,int i)
    {

        int value = Random.Range(0, objects.Length);

        // una vez salga el numero revisar que este no haya salido antes
        //Debug.Log("aparecio " + objects[j] + " es " + objects[j].name + "(Clone)");
        if (!objectsUsed.Contains(value))// si no contiene el valor la lista, entonces se agrega
        {objectsUsed.Add(value);
            PosUsed.Add(i);
            Instantiate(objects[value], pos[i].transform);           
            
        }
        else if (objectsUsed.Contains(value))// si ya lo contiene, se hace de nuevo
        {
            if (PosUsed.Contains(i))
               {randomInPlaces(pos, objects);}
            else if (!PosUsed.Contains(i))
            randomObjects(pos, objects,i);
        }

    }
   public void changeMaterial()
    {
        for(int i = 0; i< CuadroPos.Length;i++)
        {
            int randomvalue = Random.Range(0, Arte.Length);
            CuadroPos[i].GetComponent<MeshRenderer>().materials[1].mainTexture = Arte[randomvalue].mainTexture;
        }
    }
}

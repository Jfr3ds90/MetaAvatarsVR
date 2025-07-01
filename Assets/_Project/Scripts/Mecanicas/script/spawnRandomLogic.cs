
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    [SerializeField]private GameObject[] objectTotals;
    [SerializeField] private Transform[] posObject;

    [SerializeField] private Material[] Arte;
    [SerializeField] private GameObject[] CuadroPos;

    List<int> objectsUsed = new List<int>();//lista de objetos ya usados
    List<int> PosUsed = new List<int>();//lista de objetos ya usados
    private void OnEnable()
    {   
        /*if(posObject.Length>0)
        randomInPlaces(posObject,objectTotals);*/
    }
    private void Start()
    {
        changeMaterial();
        MixElements();

        /* if (CuadroPos.Length > 0)
              changeMaterial();
          if (objectsUsed.Count > 0 && objectsUsed.Count != objectTotals.Length + 1)
              randomInPlaces(posObject, objectTotals);*/
    }
    void randomInPlaces(GameObject[] pos, GameObject[] objects)//se instancia de manera aleatoria solo haciendolo 1 vez, solo falta hacer que siempre seand objetos diferentes
    {// se instancia objeto y se saca de la array
        //la posicion donde fue instanciado tambien se saca de la array
        //aleatoriedad de objetos disponibles y posiciones disponibles

        for (int i = 0; i < pos.Length; i++) //posicion aleatoria
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
                        //PosUsed.Add(i);//Debug.Log("Fue agregado " + i);
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
        /*if (objects.Length+1 != PosUsed.Count)
            randomInPlaces(pos, objects);*/

        /*
        if (objectsUsed.Count != objectTotals.Length + 1)
            randomInPlaces(posObject, objectTotals);
        */
    }
    void randomObjects(GameObject[] pos, GameObject[] objects,int i)
    {

        int value = Random.Range(0, objects.Length);

        // una vez salga el numero revisar que este no haya salido antes
        //Debug.Log("aparecio " + objects[j] + " es " + objects[j].name + "(Clone)");
        if (!objectsUsed.Contains(value))// si no contiene el valor la lista, entonces se agrega
        {
            objectsUsed.Add(value);
            PosUsed.Add(i);
            Instantiate(objects[value], pos[i].transform);
            Debug.Log("el objeto es "+ objects[value]+" y la posicion es "+ pos[i].transform+" tambien esto esta en: "+this.gameObject.name);
        }
        else if (objectsUsed.Contains(value))// si ya lo contiene, se hace de nuevo
        {
            if (PosUsed.Contains(i))
               {randomInPlaces(pos, objects);}
            /*else if (!PosUsed.Contains(i))
            randomObjects(pos, objects,i);*/
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
    void MixElements()
    {
        if (posObject.Length >= objectTotals.Length)
        {
            objectTotals = objectTotals.OrderBy(go => Random.value).ToArray();
            posObject = posObject.OrderBy(tr => Random.value).ToArray();

            for (int i = 0; i < objectTotals.Length; i++)
            {
                GameObject go = Instantiate(objectTotals[i], posObject[i].transform.position, posObject[i].transform.rotation);
                go.name = objectTotals[i].name;
            }
        }
        else
        {
            Debug.LogError("Deben existir mas pocisiones que elementos");
        }
    }
}

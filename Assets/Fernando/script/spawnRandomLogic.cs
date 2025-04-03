
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectForTable;
    //public int space; 
    public Transform tablePos;
    public GameObject[,] squares = new GameObject[10, 10];
    private int activatedX,activatedY;
    //private bool available;
    //private Dictionary<bool, GameObject[,]> availablePos;
    private void Awake()
    {
        // convendría crear una lista  sobre los numeros disponibles para la aleatoriedad
        //for (int r= 0;r< (10*10);r++)
        //{
            //if (activatedX == 11)
            //{
            //    activatedX = 0;activatedY += 1;
            //}
            for (int i = 0; i < 10; i++)
            {
            Debug.Log("inicio");
                //if (activatedX == i)
                    for (int j = 0; j < 10; j++)
                    {
                        
                        sorterOfPositions( i, j);
                Debug.Log("activacion ");

                    }
            }
            
                     
        //}

        
    }
    void sorterOfPositions( int i, int j)
    {
        int boolean = Random.Range(0, 2);
        switch (boolean)
        {
            case 0:
                Debug.Log("Slot empty (" + i + " , " + j + ")");
                break;
            case 1:
                //availablePos.Add(true, squares);
                squares[i, j] = Instantiate(objectForTable[0], tablePos.position + new Vector3(i, 0, j), Quaternion.identity);
                Debug.Log("Slot used (" + i + " , " + j + ")");
                break;
        }
    }
}

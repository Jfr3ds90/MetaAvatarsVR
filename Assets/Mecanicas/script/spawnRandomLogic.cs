
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectForTable;
    private int objectsTotal; 
    public Transform tablePos;
    public GameObject[,] squares = new GameObject[10, 10];
    private void Awake()
    {
        objectsTotal= objectForTable.Length;
        Debug.Log(objectsTotal);
            for (int i = 0; i < 10; i++)
            {
            Debug.Log("inicio");
                    for (int j = 0; j < 10; j++)
                    {
                      int randomObject =  Random.Range(0, objectsTotal);
                        sorterOfPositions( i, j, objectForTable,randomObject);
                Debug.Log("activacion "+ randomObject);

                    }
            }
    }
    void sorterOfPositions( int i, int j, GameObject[] objects, int v)
    {
        int boolean = Random.Range(0, 2);
        switch (boolean)
        {
            case 0:
                Debug.Log("Slot empty (" + i + " , " + j + ")");
                break;
            case 1:
                squares[i, j] = Instantiate(objects[v], tablePos.position + new Vector3(i, 0, j), Quaternion.identity);
                Debug.Log("Slot used (" + i + " , " + j + ")");
                break;
        }
    }
}

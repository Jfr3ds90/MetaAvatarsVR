
using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectForTable;
    private int objectsTotal; 
    public Transform tablePos;
    public Vector2 TotalArea;
    public GameObject[,] squares;
    public Vector2[] Value;
    private void Awake()
    {
        squares = new GameObject[Mathf.RoundToInt(TotalArea.x),Mathf.RoundToInt(TotalArea.y)];
    }
    private void OnEnable()
    {
        objectsTotal = objectForTable.Length;
        for (int i = 0; i < Mathf.RoundToInt(TotalArea.x); i++)
        {
            for (int j = 0; j < Mathf.RoundToInt(TotalArea.y); j++)
            {
                int randomObject = Random.Range(0, objectsTotal);

                sorterOfPositions(i, j, objectForTable, randomObject);
            }
        }
    }
    void sorterOfPositions( int i, int j, GameObject[] objects, int v)
    {
        int boolean = Random.Range(0, 2); for (int k = 0; k < Value.Length; k++)
        {
            if (Value[k].x == i && Value[k].y == j)
            {
                boolean = 0;
            }

        }
        switch (boolean)
        {
            case 0:
                Debug.Log("los valores "+i+","+j+" son vacios");
                break;
            case 1:
                squares[i, j] = Instantiate(objects[v], tablePos.position + new Vector3(i, 0, j), Quaternion.identity);
                break;
        }
    }
}

using UnityEngine;

public class spawnRandomLogic : MonoBehaviour
{
    public GameObject[] objectForTable;
    public int[] space; // menor numer = menor tamaño igual en grande
    public Transform tablePos;
    public GameObject[,] squares = new GameObject[8, 8];
    void Start()
    {
        
    }
    void Update()
    {
        
    }
    // Separar por cuadrantes y grupos de cuadrantes según la posicion deseable
    // Para ver la posicion de los cuadrantes convendría crear cuadricula tipo ajedrez
    void sorterOfPositions()
    {
        switch (objectForTable.Length)
        {
            case 0:
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        squares[i, j] = Instantiate(objectForTable[0], new Vector3(i, j, 0), Quaternion.identity);
                    }
                }
                break;
            case 1:
                break;
            case 2:
                break;
        }
    }
}

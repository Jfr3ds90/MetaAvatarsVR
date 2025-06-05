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
            sizeExample.Add(new Vector3(x, y, z));      
    }
    public void CheckR( )
    {
        for (int i = 0; i < realFigure.Count; i++)       
            for (int j = 0; j < sizeExample.Count; j++)            
                if (realFigure[i] == sizeExample[i])               
                    if (!posTimeCube.ContainsKey(sizeExample[i]))
                    posTimeCube.Add(sizeExample[i],true);

        if (posTimeCube.Count == realFigure.Count)
        {
            safe.GetComponent<Switch>().OpenDoorAct();
            FindAnyObjectByType<AudioManager>().moreAction = 2;
            FindAnyObjectByType<AudioManager>().calls();
            Debug.Log("Cantidad igual"); 
        }

    }
    public void fastSolution()
    {
        safe.GetComponent<Switch>().OpenDoorAct();
        FindAnyObjectByType<AudioManager>().moreAction = 2;
        FindAnyObjectByType<AudioManager>().calls();
    }
}

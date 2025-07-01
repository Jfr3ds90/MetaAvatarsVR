using UnityEngine;
using System.Linq;

public class spawnRandomLogic : MonoBehaviour
{
    [SerializeField] private GameObject[] objectTotals;
    [SerializeField] private Transform[] posObject;

    [SerializeField] private Material[] Arte;
    [SerializeField] private GameObject[] CuadroPos;

    private void Start()
    {
        changeMaterial();
        MixElements();
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


    public void changeMaterial()
    {
        for (int i = 0; i < CuadroPos.Length; i++)
        {
            int randomvalue = Random.Range(0, Arte.Length);
            CuadroPos[i].GetComponent<MeshRenderer>().materials[1].mainTexture = Arte[randomvalue].mainTexture;
        }
    }
}
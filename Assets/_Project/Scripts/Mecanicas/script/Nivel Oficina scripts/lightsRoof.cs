using UnityEngine;

public class lightsRoof : MonoBehaviour
{
    [SerializeField] private Material materialLight, materialDark;
    [SerializeField] private GameObject[] roof;
    public void changeMaterialRoof()
    {
        for (int i = 0; i < roof.Length; i++) 
        {
            roof[i].GetComponent<Renderer>().material = materialLight;
        }
    }
}

using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class ValveGas : MonoBehaviour
{
    public int gasType;
    [SerializeField] private float y;
    ValveManager manager;
    private void OnEnable()
    {
        manager = FindAnyObjectByType<ValveManager>();
    }
    public void OpenClose()
    {

        if (transform.eulerAngles.y ==359.99f)
        {
            manager.activatedValves[gasType] = true;
            manager.MixtureGas(gasType);
            Debug.Log("maximo");
        }
        else if (transform.eulerAngles.y == 0)
        {
            manager.activatedValves[gasType] = false;
            manager.MixtureGas(gasType);
            Debug.Log("minimo");
        }
            y = transform.eulerAngles.y;
    }
    private void Update()
    {
    }
}

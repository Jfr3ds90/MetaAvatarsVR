using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
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
        RenderSettings.fog = true;
       
        if (transform.eulerAngles.y >=350f)
        {
            manager.activatedValves[gasType] = true;
            manager.MixtureGas(gasType);
            Debug.Log("maximo");
            RenderSettings.fogDensity = 1;
        }
        else if (transform.eulerAngles.y <= 1)
        {
            manager.activatedValves[gasType] = false;
            manager.MixtureGas(gasType);
            Debug.Log("minimo");
            RenderSettings.fogDensity = 1;
        }
        Debug.Log(RenderSettings.fogDensity);
        y = transform.eulerAngles.y;
    }
    private void Update()
    {
    }
}

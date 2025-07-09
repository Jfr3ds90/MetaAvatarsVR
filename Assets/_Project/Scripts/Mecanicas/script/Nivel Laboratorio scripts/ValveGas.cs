using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class ValveGas : MonoBehaviour
{
    public int gasType;
    [SerializeField] private float z;
    ValveManager manager;
    public Color colorGas;

    public bool action;
    private void OnEnable()
    {
        manager = FindAnyObjectByType<ValveManager>();
    }
    public void OpenClose()
    {
        RenderSettings.fog = true;
        //RenderSettings.fogDensity = transform.eulerAngles.y / 350;
        z = transform.eulerAngles.z;
        if (z >=350f)
        {
            manager.activatedValves[gasType] = true;
          //  manager.MixtureGas(gasType);
            Debug.Log("maximo");
          // RenderSettings.fogDensity = 1;
        }
        else if (z <= 1)
        {
            manager.activatedValves[gasType] = false;
            //  manager.MixtureGas(gasType);
            StopCoroutine(manager.GasActivated());
            Debug.Log("minimo");
          //  RenderSettings.fogDensity = 0;
        }

        if (z >= 1)
        {
            manager.colorGas += (z)*(colorGas);
            //manager.gasFog = y;
        }
        manager.MixtureGas(gasType);
        manager.GasAction();
        //Debug.Log(RenderSettings.fogDensity+" es la densidad "+ manager.gasFog / 350);
       
    }
    private void Update()
    {
        Debug.Log(transform.eulerAngles + " es la rotacion");
        if (action==true)
       { if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z + Time.deltaTime * 20);
            OpenClose();
        }
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z - Time.deltaTime * 20);
                OpenClose();
            }
        }
    }
}

using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class ValveGas : MonoBehaviour
{
    public int gasType;
    [SerializeField] private float y;
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
        y = transform.eulerAngles.y;
        if (y >=350f)
        {
            manager.activatedValves[gasType] = true;
          //  manager.MixtureGas(gasType);
            Debug.Log("maximo");
          // RenderSettings.fogDensity = 1;
        }
        else if (y <= 1)
        {
            manager.activatedValves[gasType] = false;
            //  manager.MixtureGas(gasType);
            StopCoroutine(manager.GasActivated());
            Debug.Log("minimo");
          //  RenderSettings.fogDensity = 0;
        }

        if (y >= 1)
        {
            manager.colorGas += colorGas;
            //manager.gasFog = y;
        }
        manager.MixtureGas(gasType);
        manager.GasAction();
        //Debug.Log(RenderSettings.fogDensity+" es la densidad "+ manager.gasFog / 350);
       
    }
    private void Update()
    {
        if(action==true)
       { if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y+Time.deltaTime*20, 0);
            OpenClose();
        }
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y - Time.deltaTime*20, 0);
                OpenClose();
            }
        }
    }
}

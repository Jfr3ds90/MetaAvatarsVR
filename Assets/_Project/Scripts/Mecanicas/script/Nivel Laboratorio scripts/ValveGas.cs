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

        z = transform.eulerAngles.z;

        if(manager.colorGas == Color.gray)
            manager.colorGas = manager.lastColor;

        if (z >=350f)
        {
            manager.activatedValves[gasType] = true;

            Debug.Log("maximo");

        }
        else if (z <= 10)
        {
            manager.activatedValves[gasType] = false;

            if (colorGas == Color.red)
                manager.colorGas = new Color(0, manager.colorGas.g, manager.colorGas.b);
            else if (colorGas == Color.green)
                manager.colorGas = new Color(manager.colorGas.r, 0, manager.colorGas.b);
            else if (colorGas == Color.blue)
                manager.colorGas = new Color(manager.colorGas.r, manager.colorGas.g, 0);

            StopCoroutine(manager.GasActivated());

            Debug.Log("minimo");
          //  RenderSettings.fogDensity = 0;
        }
       
        if (z > 10)
        {
            manager.colorGas = /*(z)*/(colorGas)+ manager.colorGas;
            manager.colorGas.a = 1;
            //manager.gasFog = y;
        }

        if (manager.colorGas == Color.black)
            manager.colorGas = Color.gray;

        manager.MixtureGas(gasType);
        manager.GasAction();
        //Debug.Log(RenderSettings.fogDensity+" es la densidad "+ manager.gasFog / 350);

    }
    private void Update()
    {
        //Debug.Log(transform.eulerAngles + " es la rotacion");
        if (action==true)
       { if(Input.GetKey(KeyCode.RightArrow))
        {
                manager.stepActivationGas = true;
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

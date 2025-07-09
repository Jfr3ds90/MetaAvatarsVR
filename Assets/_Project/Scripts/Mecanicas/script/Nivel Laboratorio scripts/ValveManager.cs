using System.Collections;
using UnityEngine;

public class ValveManager : MonoBehaviour
{
    public bool[] activatedValves;
    int lastValveActived;
    public static float gasFog;
    public Color colorGas;
    Chemistry chemstry;
    private void OnEnable()
    {
        chemstry = FindAnyObjectByType<Chemistry>();
    }
    public void MixtureGas(int value)
    {
        int n = 0;
        for (int i = 0; i < activatedValves.Length; i++) 
        {
            if (activatedValves[i] == true)
                n++;
            //else if (activatedValves[i] == false)
               // n--;
        }
        if (n > 2)
        {
            gasFog += Time.deltaTime * 0.01f*n;//hacer corrutina para esto
            Debug.Log("más de 2 valvulas abiertas"); 
        }

        else
        {
            if (n == 1)
            {
                if(lastValveActived!=-1&&lastValveActived==value)
                {
                    switch (value)
                    {
                        case 0:
                            Debug.Log("valvula activa " + value);
                            break;
                        case 1:
                            Debug.Log("valvula activa " + value);
                            break;
                        case 2:
                            Debug.Log("valvula activa " + value);
                            break;
                        case 3:
                            Debug.Log("valvula activa " + value);
                            break;
                        default:
                            gasFog += Time.deltaTime*10f;
                            break;
                    }
                    lastValveActived = n;
                    //RenderSettings.fogDensity = gasFog;//vincularlo al valor y de las valvulas
                }
                else
                {
                    for (int i = 0;i < activatedValves.Length;i++)
                        if (activatedValves[i] == true)
                        {
                            lastValveActived=i; break;
                        }    
                }
                    
            }
            else if (n == 2)
            {
                switch (value)
                {
                    case 0:
                        Debug.Log("valvula activa " + value);
                        break;
                    case 1:
                        Debug.Log("valvula activa " + value+" y tambien "+lastValveActived);
                        break;
                    case 2:
                        Debug.Log("valvula activa " + value + " y tambien " + lastValveActived);
                        break;
                    case 3:
                        Debug.Log("valvula activa " + value + " y tambien " + lastValveActived);
                        break;
                    default:
                        gasFog += Time.deltaTime * 20f;
                        break;
                }
                lastValveActived = -1;
            }
            else
                lastValveActived = -1;
        }

        
    }
    public void GasAction()
    {
        RenderSettings.fogDensity = gasFog / 350;
        StartCoroutine(GasActivated());
    }
    public IEnumerator GasActivated()
    {while (true) 
        {
            gasFog += Time.deltaTime * 0.1f;
            RenderSettings.fogDensity = gasFog / 350;

            RenderSettings.fogColor = new Vector4(colorGas.r* RenderSettings.fogDensity,
                colorGas.g* RenderSettings.fogDensity, 
                colorGas.b* RenderSettings.fogDensity, 
                1);
            if(colorGas.r>255) colorGas.r = 255;
            if(colorGas.g>255) colorGas.g = 255;
            if(colorGas.b>255) colorGas.b = 255;
            for(int i = 0;i<chemstry.particleSystems.Length;i++)
            {
                //chemstry.particleSystems[i].main.startColor = colorGas;
                ParticleSystem.ColorOverLifetimeModule COL =chemstry.particleSystems[i].colorOverLifetime ;
                COL.color = new ParticleSystem.MinMaxGradient(colorGas).color;
              //  COL.color = new ParticleSystem.MinMaxGradient(new Vector4(0,0,0,0)).gradientMax;
                Debug.Log(COL+" es el color");
                if (!chemstry.particleSystems[i].isPlaying)
                    chemstry.particleSystems[i].Play();
            }
          //  Debug.Log(RenderSettings.fogDensity + " es la densidad y el color es "+ RenderSettings.fogColor);
            yield return null;
        }
    }
}

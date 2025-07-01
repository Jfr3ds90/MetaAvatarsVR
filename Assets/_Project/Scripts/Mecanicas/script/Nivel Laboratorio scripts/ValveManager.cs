using UnityEngine;

public class ValveManager : MonoBehaviour
{
    public bool[] activatedValves;
    int lastValveActived;
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
            Debug.Log("más de 2 valvulas abiertas");

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
                            break;
                    }
                    lastValveActived = n;
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
                        break;
                }
                lastValveActived = -1;
            }
            else
                lastValveActived = -1;
        }

        
    }
}

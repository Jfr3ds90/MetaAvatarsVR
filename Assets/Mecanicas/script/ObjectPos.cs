using UnityEngine;

public class ObjectPos : MonoBehaviour
{
    public bool correctPos;
    public int IdPos;// por ahora colocar manualmente la idpos pero nunca puede haber más de 1 con un mismo numero
    private void Awake()
    {
        correctPos = false;       
    }
}

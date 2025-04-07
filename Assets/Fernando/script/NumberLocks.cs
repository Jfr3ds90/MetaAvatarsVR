using UnityEngine;

public class NumberLocks : MonoBehaviour
{// opcion por fisica, habria que colocar un detector para el numero que pasa en frente para que se detenga por movimiento
// opcion por codigo, 1 interaccion = un mov de candado | por cada 36°
    [SerializeField]GameObject numberLock1, numberLock2, numberLock3;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void rotationOfLock(GameObject gameObject,GameObject hand)
    {
       // gameObject.transform.rotation += hand.transform.rotation;
    }
}

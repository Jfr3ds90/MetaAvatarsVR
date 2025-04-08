using UnityEngine;

public class NumberLocks : MonoBehaviour
{// opcion por fisica, habria que colocar un detector para el numero que pasa en frente para que se detenga por movimiento
// opcion por codigo, 1 interaccion = un mov de candado | por cada 36°
    public bool lockMove;
    public NumberLocks otherNumberLock1,otherNumberLock2;
    public GameObject lockAnimation;
    [SerializeField]private int valueKey,value,unit;
    private void OnEnable()
    {
        lockMove = false;
        valueKey = Random.Range(0, 10);
        value = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if(unit==1)
            if (Input.GetKeyUp(KeyCode.F))
                rotationOfLock();

        if(unit == 2)
            if (Input.GetKeyUp(KeyCode.G))
                rotationOfLock();

        if (unit == 3)
            if (Input.GetKeyUp(KeyCode.H))
                rotationOfLock();

    }
    public void rotationOfLock()
    {   if(lockMove== false || otherNumberLock1.lockMove == false || otherNumberLock2.lockMove == false)
        {
            value += 1;
            transform.Rotate(transform.rotation.x, transform.transform.rotation.y + 36, transform.rotation.z);
        }

        if (value >= 10)
            value = 0;

        if (value == valueKey)
            lockMove = true;

        if (lockMove == true && otherNumberLock1.lockMove == true && otherNumberLock2.lockMove == true)
            lockAnimation.transform.localPosition = new(0, 0, 0);
    }
}

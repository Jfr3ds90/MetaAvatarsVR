using Unity.VisualScripting;
using UnityEngine;

public class Chemistry : MonoBehaviour
{
    public int element;
    private bool isSelected = false;
    private void OnCollisionEnter(Collision collision)
    {
        var otherElement= collision.gameObject.GetComponent<Chemistry>().element;
        if(isSelected==true)
        switch(this.element)
            {
            case 0:
                    switch (otherElement)
                    {
                        case 0:
                            Debug.Log("mismo elemento");                        
                            break;
                        case 1:
                            Debug.Log("combinado con el elemento 1");
                            break;
                        case 2:
                            Debug.Log("combinado con el elemento 2");
                            break;
                        default:
                        break;
                    }
                break;
            case 1:
                    switch (otherElement)
                    {
                        case 0:
                            Debug.Log("combinado con el elemento 0");
                            break;
                        case 1:
                            Debug.Log("mismo elemento");
                            break;
                        case 2:
                            Debug.Log("combinado con el elemento 2");
                            break;
                        default:
                            break;
                    }
                    break;
            case 2:
                    switch (otherElement)
                    {
                        case 0:
                            Debug.Log("combinado con el elemento 0");
                            break;
                        case 1:
                            Debug.Log("combinado con el elemento 1");
                            break;
                        case 2:
                            Debug.Log("mismo elemento");
                            break;
                        default:
                            break;
                    }
                    break;
            default:
                break; 
            }
    }
    public void onGrab(bool selection)
    {
        isSelected=selection;
    }
}

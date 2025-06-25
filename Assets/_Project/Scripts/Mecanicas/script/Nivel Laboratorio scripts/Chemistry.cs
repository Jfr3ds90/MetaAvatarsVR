using Unity.VisualScripting;
using UnityEngine;

public class Chemistry : MonoBehaviour
{
    public int element;
    public bool isSelected;

    private void OnTriggerEnter(Collider collision)
    {
        if(collision.gameObject.GetComponent<Chemistry>()!=null)
       { 
            var otherElement= collision.gameObject.GetComponent<Chemistry>().element;
        if(isSelected==true)
                switch (this.element)
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
    }
    public void onGrab()
    {
        Debug.Log(isSelected);
        isSelected= !isSelected;
        Debug.Log("esto funciona");
        Debug.Log(GetComponent<BoxCollider>()+" esta presente");
        GetComponent<BoxCollider>().isTrigger = isSelected;
    }
}

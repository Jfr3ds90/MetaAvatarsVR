using Unity.VisualScripting;
using UnityEngine;

public class Chemistry : MonoBehaviour
{
    public int element;
    public bool isSelected;
    [SerializeField] private Switch LeftDoor,RightDoor;
    [SerializeField] private MeshRenderer Chem;
    private Color color;
    private void OnTriggerEnter(Collider collision)
    {
        /* if(collision.gameObject.GetComponent<Chemistry>()!=null)
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
         else*/
        Debug.LogWarning(Chem.material.GetColor("_TopColor")+" es el color y el correcto es "+color);
        if (collision.gameObject.tag=="cubePuzzle")
            if( Chem.material.GetColor("_TopColor")==FindAnyObjectByType<ValveManager>().keyColor)
        {
            LeftDoor.OpenDoorAct();
            RightDoor.CloseDoorAct();
        }
        if(collision.gameObject.tag == "Finish")
                Chem.material.SetFloat("_Fill", 0);
    }
    private void OnParticleCollision(GameObject other)
    {
        Debug.LogWarning("detecto particulas");
        if (other != null)
            Debug.Log(other.name + " es la particula que choco");
        //Chem.enabled = true;
        color = FindAnyObjectByType<ValveManager>().colorGas;
        
        Chem.material.SetColor("_SideColor",new Color(color.r*0.3f,color.g * 0.3f, color.b * 0.3f, color.a));
        Chem.material.SetColor("_TopColor",color);
        Chem.material.SetFloat("_Fill",0.271f);
    }
    public void onGrab()
    {
        isSelected= !isSelected;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            LeftDoor.OpenDoorAct();
            RightDoor.CloseDoorAct();
        }
    }
}

using Unity.VisualScripting;
using UnityEngine;
using HackMonkeys.Debugging;

public class Chemistry : MonoBehaviour
{
    public int element;
    public bool isSelected;
    [SerializeField] private Switch LeftDoor,RightDoor;
    [SerializeField] private MeshRenderer Chem;
    [SerializeField] private AudioClip Fill, Drop;
    private Color color;
    private AudioSource audioS;
    private void Awake()
    {
        audioS = GetComponent<AudioSource>();
    }
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
                                 AdvancedDebugSystem.Log("mismo elemento", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 1:
                                 AdvancedDebugSystem.Log("combinado con el elemento 1", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 2:
                                 AdvancedDebugSystem.Log("combinado con el elemento 2", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             default:
                                 break;
                         }
                         break;
                     case 1:
                         switch (otherElement)
                         {
                             case 0:
                                 AdvancedDebugSystem.Log("combinado con el elemento 0", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 1:
                                 AdvancedDebugSystem.Log("mismo elemento", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 2:
                                 AdvancedDebugSystem.Log("combinado con el elemento 2", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             default:
                                 break;
                         }
                         break;
                     case 2:
                         switch (otherElement)
                         {
                             case 0:
                                 AdvancedDebugSystem.Log("combinado con el elemento 0", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 1:
                                 AdvancedDebugSystem.Log("combinado con el elemento 1", LogCategory.Avatar, LogLevel.Debug);
                                 break;
                             case 2:
                                 AdvancedDebugSystem.Log("mismo elemento", LogCategory.Avatar, LogLevel.Debug);
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
        AdvancedDebugSystem.LogWarning(Chem.material.GetColor("_TopColor")+" es el color y el correcto es "+color+" y el objeto que toco es "+collision.gameObject.name+" con el tag "+collision.tag, LogCategory.Avatar);
        if (collision.gameObject.tag=="cubePuzzle")
            if( Chem.material.GetColor("_TopColor")==FindAnyObjectByType<ValveManager>().keyColor)
        {
            LeftDoor.OpenDoorAct();
            RightDoor.CloseDoorAct();
        }
        if(collision.gameObject.tag == "Finish")
        {
            Chem.material.SetFloat("_Fill", 0);

                audioS.clip = Drop;
                audioS.Play();
        }
        if (collision.gameObject.tag == "Player")
            AdvancedDebugSystem.LogWarning("Detecto al jugador", LogCategory.Avatar);
    }
    private void OnParticleCollision(GameObject other)
    {
        AdvancedDebugSystem.LogWarning("detecto particulas", LogCategory.Avatar);
        if (other != null)
            AdvancedDebugSystem.Log(other.name + " es la particula que choco", LogCategory.Avatar, LogLevel.Debug);
        //Chem.enabled = true;
        color = FindAnyObjectByType<ValveManager>().colorGas;
        
        Chem.material.SetColor("_SideColor",new Color(color.r*0.3f,color.g * 0.3f, color.b * 0.3f, color.a));
        Chem.material.SetColor("_TopColor",color);
        Chem.material.SetFloat("_Fill",0.271f);
        audioS.clip = Fill;
        audioS.Play();
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

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player")
            AdvancedDebugSystem.LogWarning("Detecto al jugador", LogCategory.Avatar);
    }

}

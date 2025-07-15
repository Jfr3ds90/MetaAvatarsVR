using Unity.VisualScripting;
using UnityEngine;

public class Chemistry : MonoBehaviour
{
    public int element;
    public bool isSelected;
    [SerializeField] private Switch LeftDoor,RightDoor;
    [SerializeField] private MeshRenderer Chem;
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
        else if (collision.gameObject.tag=="cubePuzzle")
        {
            LeftDoor.OpenDoorAct();
            RightDoor.CloseDoorAct();
        }
    }
    private void OnParticleCollision(GameObject other)
    {
        if (other != null)
            Debug.Log(other.name + " es la particula que choco");
        Color color = other.GetComponent<ParticleSystem>().main.startColor.color;
        Chem.material.color = color;

    }
    private void OnParticleTrigger()
    {
        Debug.Log(name+" es quien detecto");
    }
    private void OnEnable()
    {
        //FindAnyObjectByType<ParticleSystem>().Play();
        Debug.Log(FindAnyObjectByType<ParticleSystem>().name+" es la particula encontrada");
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

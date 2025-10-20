using HackMonkeys.UI.Spatial;
using Oculus.Interaction;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class RayToText : MonoBehaviour
{
    RayInteractor rayI;

    VRInputFieldCursorController vrfcc;

    List<float> list = new List<float>();
    private void Awake()
    {
        rayI = GetComponent<RayInteractor>();
        vrfcc = FindAnyObjectByType<VRInputFieldCursorController>();
        //=FindAnyObjectByType<TMP_Text>();
    }

    private void Update()
    {
        CheckActivate();
       /* if (Physics.Raycast(ray, out RaycastHit hits)&&
            hits.collider.gameObject.GetComponent<TMP_InputField>())
        {
       MEJORAR PARA LLAMAR SOLO CUANDO SE PRESIONE EL BOTON CORRESPONDIENTE
        }*/
    }
    void CheckActivate()
    {
      if ( FindAnyObjectByType<VRInputFieldCursorController>().activation==true)
        {
            Ray ray = rayI.Ray;
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    //tex.text = hit.collider.gameObject.GetComponent<TMP_InputField>().text;   
                    //var sc = TMP_TextUtilities.FindIntersectingCharacter(tex, ray.direction,FindAnyObjectByType<Camera>(),true);
                  /*  Debug.LogWarning(hit.collider.gameObject.GetComponent<TMP_InputField>().name + " se ha detectado y el rayo esta en " + hit.transform.localPosition
                        + " pos inicial " + ray.origin + " pos final " + hit.point);*/

                    var dict = FindAnyObjectByType<VRInputFieldCursorController>().CharP;
                    int i = 0;

                    

                    list.Clear();

                    TMP_CharacterInfo lastChar = new TMP_CharacterInfo();
                    bool exist = false;
                    foreach (var item in dict)
                    {
                        Debug.Log("Esta en la llave " + item.Key.character);

                        Vector3 pos = item.Value;
                        list.Add(pos.x);
                        if (hit.point.x >= pos.x && pos.x >= list[i])
                        { 
                            lastChar = item.Key; 
                            exist = true; 
                        }
                        else if (hit.point.x >= list[0] && hit.point.x < list[1])
                        { 
                            Debug.LogWarning("Choco con la letra correcta la cual es " + item.Key.character+ " siendo la letra "+ char.GetNumericValue(item.Key.character));
                            FindAnyObjectByType<VRInputFieldCursorController>().activation = false;
                        //falta agregar un indicativo visual para dar a entender que esa letra esta seleccionada, quizas sirva con | en vez de un prefab

                        var VK3D = FindAnyObjectByType<VirtualKeyboard3D>();

                       // VK3D.OnSelectText(); Descomentar esta linea para seguir viendo como mejorar el teclado o simplemente adaptarlo de otra manera

                        // a partir del caracter colocado, se elimine para colocar las siguientes letras...
                        //colocar que a partir de la letra en adelante se coloquen las siguientes letras dejando todo lo siguiente sin borrar (por lo menos para el usuario)
                        }
                        else 
                        {   
                            Debug.LogWarning("no detecto letra y debe de haber seleccionado a la izquierda"); 
                        }
                            i++;
                    }
                    if (exist == true)
                        { Debug.LogWarning("Choco con la letra correcta la cual es " + lastChar.character); FindAnyObjectByType<VRInputFieldCursorController>().activation = false; }
                else
                    {
                    FindAnyObjectByType<VRInputFieldCursorController>().activation = false;
                    }
                }
           
        }
    }
}

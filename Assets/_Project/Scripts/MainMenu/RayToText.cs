using Oculus.Interaction;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RayToText : MonoBehaviour
{
    RayInteractor rayI;

    VRInputFieldCursorController vrfcc;
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

                    List<float> list = new List<float>();

                    list.Clear();

                    TMP_CharacterInfo lastChar = new TMP_CharacterInfo();
                    bool exist = false;
                    foreach (var item in dict)
                    {
                        Debug.Log("Esta en la llave " + item.Key.character);

                        Vector3 pos = item.Value;
                        list.Add(pos.x);
                        if (hit.point.x >= pos.x && pos.x >= list[i])
                        { lastChar = item.Key; exist = true; }
                        else if (hit.point.x >= list[0] && hit.point.x < list[1])
                        { Debug.LogWarning("Choco con la letra correcta la cual es " + item.Key.character); FindAnyObjectByType<VRInputFieldCursorController>().activation = false; }
                        else { Debug.LogWarning("no detecto letra y debe de haber seleccionado a la izquierda"); }
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

using Oculus.Interaction;
using System.Collections.Generic;
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
    {Ray ray = rayI.Ray;
            Debug.DrawRay(ray.origin,ray.direction,Color.red,10);
        if(Input.GetKeyUp(KeyCode.V))
            {
            
            if (Physics.Raycast(ray, out RaycastHit hit))
                {
                //tex.text = hit.collider.gameObject.GetComponent<TMP_InputField>().text;   
                //var sc = TMP_TextUtilities.FindIntersectingCharacter(tex, ray.direction,FindAnyObjectByType<Camera>(),true);
                Debug.LogWarning(hit.collider.gameObject.GetComponent<TMP_InputField>().name + " se ha detectado y el rayo esta en "+hit.transform.localPosition
                    +" pos inicial "+ ray.origin+" pos final "+ hit.point);
                var dict = hit.collider.gameObject.GetComponent<VRInputFieldCursorController>().CharP;
                int i = 0;
                /*foreach (var item in dict)
                {
                    Vector3 pos = dict[i].ToVector3f();
                    if (hit.point == )
                        Debug.LogWarning("Choco con la letra correcta");
                    i++;
                }*/
                }//colocar la posición exacta en el mundo, en donde choca
            }

       /* if (Physics.Raycast(ray, out RaycastHit hits)&&
            hits.collider.gameObject.GetComponent<TMP_InputField>())
        {
       MEJORAR PARA LLAMAR SOLO CUANDO SE PRESIONE EL BOTON CORRESPONDIENTE
        }*/
    }
}

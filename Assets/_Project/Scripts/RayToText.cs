using Oculus.Interaction;
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
        if(Input.GetKeyUp(KeyCode.V))
            {
            Ray ray = rayI.Ray;
            if (Physics.Raycast(ray, out RaycastHit hit))
                {
                //tex.text = hit.collider.gameObject.GetComponent<TMP_InputField>().text;
                //var sc = TMP_TextUtilities.FindIntersectingCharacter(tex, ray.direction,FindAnyObjectByType<Camera>(),true);
                Debug.LogWarning(hit.collider.gameObject.GetComponent<TMP_InputField>().characterValidation + " se ha detectado y el rayo esta en "+hit.transform.position); 
                }//colocar la posición exacta en el mundo, en donde choca
            }
    }
}

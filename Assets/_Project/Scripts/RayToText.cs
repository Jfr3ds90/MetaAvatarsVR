using Oculus.Interaction;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RayToText : MonoBehaviour
{
    RayInteractor rayI;
    TMP_Text tex;//agregar al prefab
    private void Awake()
    {
        rayI = GetComponent<RayInteractor>();
        tex=gameObject.AddComponent<TMP_Text>();
    }

    private void Update()
    {
        if(Input.GetKeyUp(KeyCode.V))
            {
            Ray ray = rayI.Ray;
            if (Physics.Raycast(ray, out RaycastHit hit))
                {tex.text = hit.collider.gameObject.GetComponent<TMP_InputField>().text;
                var sc = TMP_TextUtilities.FindIntersectingCharacter(tex, ray.direction,FindAnyObjectByType<Camera>(),true);
                Debug.LogWarning(sc + " se ha detectado "); 
                }
            }
    }
}

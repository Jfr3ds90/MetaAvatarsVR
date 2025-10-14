using TMPro;
using UnityEngine;

public class CopyInputText : MonoBehaviour
{
    public GameObject OText,TText;//falta asignarlo al prefab del canvas

    private void OnEnable()
    {
      OText = FindAnyObjectByType<VRInputFieldCursorController>().gameObject;
      TText = GetComponentInChildren<TMP_Text>().gameObject;
    }
    private void Update()
    {
        TText.GetComponent<TMP_Text>().text = OText.GetComponent<TMP_InputField>().text;
    }
}

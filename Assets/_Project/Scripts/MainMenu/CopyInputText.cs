using HackMonkeys.UI.Spatial;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CopyInputText : MonoBehaviour
{
    /*[HideInInspector]*/public GameObject OText;//falta asignarlo al prefab del canvas
    public TMP_Text TText;
    public string user;

    /*[HideInInspector] public VRInputFieldCursorController vrfcc;
    [HideInInspector] public PointerEventData pointer;
    [HideInInspector] public bool clickAsigned = false;*/
    private void OnEnable()
    {
      OText = FindAnyObjectByType<VRInputFieldCursorController>().gameObject;
        TText = GetComponentInChildren<TMP_Text>();
       // fieldInteractable = GetComponent<RayInteractable>();
       // vrfcc = FindAnyObjectByType<VRInputFieldCursorController>();
    }
    private void Update()
    {
        user = OText.GetComponentInChildren<TMP_Text>().text;      
        TText.text = user;
        GetComponent<InteractableButton3D>().SetButtonLabel(user);
        Check();
    }
    void Check()
    {

    }
}

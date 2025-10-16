using HackMonkeys.UI.Spatial;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CopyInputText : MonoBehaviour
{
    /*[HideInInspector] */public GameObject OText,TText;//falta asignarlo al prefab del canvas
    public TMP_Text tex;

    /*[HideInInspector] public VRInputFieldCursorController vrfcc;
    [HideInInspector] public PointerEventData pointer;
    [HideInInspector] public bool clickAsigned = false;*/
    private void OnEnable()
    {
      OText = FindAnyObjectByType<VRInputFieldCursorController>().gameObject;
      TText = GetComponent<TMP_InputField>().gameObject;
        tex = GetComponentInChildren<TMP_Text>();
       // fieldInteractable = GetComponent<RayInteractable>();
       // vrfcc = FindAnyObjectByType<VRInputFieldCursorController>();
    }
    private void Update()
    {
        TText.GetComponent<InteractableButton3D>().SetButtonLabel(OText.GetComponentInChildren<TMP_Text>().text) ;
        tex.text = OText.GetComponent<TMP_InputField>().text;
       // TText.GetComponent<InteractableButton3D>().
       // Check();
    }
    /*void Check()
    {
        var val = OText.GetComponent<InteractableButton3D>();
        
        if (fieldInteractable == null) return;
        
        var rayInteractors = FindObjectsOfType<RayInteractor>();

        foreach (var interactor in rayInteractors)
        {
            bool isCurrentlyHovering = false;

            if (interactor.HasCandidate &&
                interactor.CandidateProperties is RayInteractor.RayCandidateProperties props &&
                props.ClosestInteractable == fieldInteractable)
            {
                isCurrentlyHovering = true;

                bool wasHovering = val._hoveredInteractors.ContainsKey(interactor) && val._hoveredInteractors[interactor];

                if (!wasHovering)
                {
                        val.OnHoverEnter();
                }

                // Detectar clic para focus
                if (!val._isFocused && interactor.State == InteractorState.Select)
                {
                    // Verificar que es un nuevo clic
                    InteractorState previousState = val._hoveredInteractors.ContainsKey(interactor) ?
                        InteractorState.Normal : InteractorState.Normal;

                    if (wasHovering) // Solo si ya estaba hovering
                    {
                        val.Focus();
                    }
                    if (val.clickAsigned == false)
                    {
                        val.pointer = new PointerEventData(EventSystem.current);
                        val.clickAsigned = true;
                    }
                    if (val.clickAsigned == true)
                    {
                        val.vrfcc.OnPointerClick(val.pointer);
                        val.vrfcc.activation = true;
                    }
                }
            }
            else if (val._hoveredInteractors.ContainsKey(interactor) && val._hoveredInteractors[interactor])
            {
                if (val._activeInteractor == interactor)
                {
                    val.OnHoverExit();
                    val._activeInteractor = null;
                }
            }
            val._hoveredInteractors[interactor] = isCurrentlyHovering;

            if (interactor.State == InteractorState.Select)
                if (val.clickAsigned == true)
                {
                    val.vrfcc.OnPointerClick(val.pointer);
                    val.vrfcc.activation = true;
                }
        }

        // Mejorar detección de clics fuera
        if (val._isFocused && val._keyboardManager != null)
        {
            /*bool shouldUnfocus = false;

            foreach (var interactor in rayInteractors)
            {
                if (interactor.State == InteractorState.Select)
                {
                    if (interactor.HasCandidate)
                    {
                        var props = interactor.CandidateProperties as RayInteractor.RayCandidateProperties;
                        if (props != null && props.ClosestInteractable != fieldInteractable)
                        {
                            // Verificar si el clic es en el teclado virtual
                            var clickedInteractable = props.ClosestInteractable;
                            var button = clickedInteractable.GetComponentInParent<InteractableButton3D>();
                            var keyboard = button?.GetComponentInParent<VirtualKeyboard3D>();

                            /*if (keyboard == null)
                            {
                                // No es el teclado, deberíamos desfocar
                                shouldUnfocus = true;
                                break;
                            }
                        }
                    }
                   /* else
                    {
                        // Clic en el vacío
                        shouldUnfocus = true;
                        break;
                    }
                }
            }

            /*if (shouldUnfocus)
            {
                val.Unfocus();
            }
        }
        val.IsFocused();
    }*/
}

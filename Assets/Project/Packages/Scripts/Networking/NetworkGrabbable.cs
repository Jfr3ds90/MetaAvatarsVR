using Fusion;
using UnityEngine;
using Oculus.Interaction;

public class NetworkGrabbable : NetworkBehaviour, IStateAuthorityChanged
{
    private Grabbable grabbable;
    private Rigidbody rb;
    [Networked] NetworkBool IsKinematic { get; set; }

    private bool isGrabbed;
    private bool previousKinematicState;

    public override void Spawned()
    {
        grabbable = GetComponent<Grabbable>();
        rb = GetComponent<Rigidbody>();

        grabbable.WhenPointerEventRaised += Grabbable_WhenPointerEventRaised;
        
        // Inicializar el estado físico
        previousKinematicState = IsKinematic;
        ToggleIsKinematic();
    }

    public override void Render()
    {
        // Detectar cambios en IsKinematic
        if (previousKinematicState != IsKinematic)
        {
            ToggleIsKinematic();
            previousKinematicState = IsKinematic;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => grabbable.WhenPointerEventRaised -= Grabbable_WhenPointerEventRaised;

    private void Grabbable_WhenPointerEventRaised(PointerEvent events)
    {
        if (events.Type == PointerEventType.Select)
        {
            if (Object.HasStateAuthority) OnGrab();
            else Object.RequestStateAuthority();
        }
        else if (events.Type == PointerEventType.Unselect)
            if (Object.HasStateAuthority && !isGrabbed) IsKinematic = false;
    }

    private void ToggleIsKinematic()
    {
        rb.isKinematic = IsKinematic;
        rb.useGravity = !IsKinematic;
    }

    public void StateAuthorityChanged()
    {
        if (isGrabbed && !Object.HasStateAuthority)
        {
            isGrabbed = false;

            grabbable.enabled = false;
            grabbable.enabled = true;
        }
        else if (Object.HasStateAuthority) OnGrab();
    }

    private void OnGrab()
    {
        isGrabbed = true;
        IsKinematic = true;
    }
}
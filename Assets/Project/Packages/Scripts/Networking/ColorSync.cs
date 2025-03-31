using Fusion;
using UnityEngine;

public class ColorSync : NetworkBehaviour
{
    [SerializeField] private MeshRenderer[] meshRenderers;
    [SerializeField] private Color customColor;

    [Networked] private NetworkColor NetworkedColor { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority) SetRandomColor();
        else ColorUpdate(); // Actualizar el color inicial para los clientes
    }

    // Se ejecuta cuando los datos sincronizados cambian
    public override void Render()
    {
        ColorUpdate();
    }

    public void SetCustomColor()
    {
        if (Object.HasStateAuthority) 
        {
            NetworkedColor = new NetworkColor(customColor);
            ColorUpdate();
        }
        else RPC_SetCustomColor();
    }

    public void SetRandomColor()
    {
        if (Object.HasStateAuthority) 
        {
            NetworkedColor = new NetworkColor(new Color(Random.value, Random.value, Random.value));
            ColorUpdate();
        }
        else RPC_SetRandomColor();
    }

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false)]
    private void RPC_SetCustomColor() => SetCustomColor();

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false)]
    private void RPC_SetRandomColor() => SetRandomColor();

    private void ColorUpdate()
    {
        Color actualColor = NetworkedColor.ToColor();
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && renderer.material != null)
                renderer.material.color = actualColor;
        }
    }
}

// Estructura auxiliar para sincronizar colores en la red
public struct NetworkColor : INetworkStruct
{
    public float R;
    public float G;
    public float B;
    public float A;

    public NetworkColor(Color color)
    {
        R = color.r;
        G = color.g;
        B = color.b;
        A = color.a;
    }

    public Color ToColor()
    {
        return new Color(R, G, B, A);
    }
}
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class SlideSync : NetworkBehaviour
{
    [SerializeField] private Image slideImage;
    [SerializeField] private Sprite[] slideSprites;

    [Networked] private int NetworkedImageIndex { get; set; }
    private int previousImageIndex = -1;

    public override void Spawned()
    {
        UpdateSlide();
        previousImageIndex = NetworkedImageIndex;
    }

    public override void Render()
    {
        if (previousImageIndex != NetworkedImageIndex)
        {
            UpdateSlide();
            previousImageIndex = NetworkedImageIndex;
        }
    }

    public void NextSlide()
    {
        if (Object.HasStateAuthority) NetworkedImageIndex = Mathf.Min(NetworkedImageIndex + 1, slideSprites.Length - 1);
        else RPC_NextSlide();
    }

    public void PreviousSlide()
    {
        if (Object.HasStateAuthority) NetworkedImageIndex = Mathf.Max(NetworkedImageIndex - 1, 0);
        else RPC_PreviousSlide();
    }

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Reliable)]
    private void RPC_NextSlide() => NextSlide();

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Reliable)]
    private void RPC_PreviousSlide() => PreviousSlide();

    private void UpdateSlide()
    {
        if (slideImage != null && slideSprites != null && slideSprites.Length > 0 && NetworkedImageIndex >= 0 && NetworkedImageIndex < slideSprites.Length)
            slideImage.sprite = slideSprites[NetworkedImageIndex];
    }
}
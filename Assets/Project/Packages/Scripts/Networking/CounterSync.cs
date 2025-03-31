using Fusion;
using TMPro;
using UnityEngine;

public class CounterSync : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI counterText;
    [SerializeField] private AudioSource audioSource;

    [Networked] private int NetworkedCounter { get; set; }
    private int previousCounterValue = 0;

    public override void Spawned()
    {
        UpdateCounterText();
        previousCounterValue = NetworkedCounter;
    }

    public override void Render()
    {
        if (previousCounterValue != NetworkedCounter)
        {
            UpdateCounterText();
            previousCounterValue = NetworkedCounter;
        }
    }

    public void IncreaseCounter()
    {
        if (Object.HasStateAuthority) NetworkedCounter++;
        else RPC_IncreaseCounter();
    }

    public void DecreaseCounter()
    {
        if (Object.HasStateAuthority) NetworkedCounter--;
        else RPC_DecreaseCounter();
    }

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Reliable)]
    private void RPC_IncreaseCounter() => IncreaseCounter();

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Reliable)]
    private void RPC_DecreaseCounter() => DecreaseCounter();

    private void UpdateCounterText()
    {
        counterText.text = NetworkedCounter.ToString();
        audioSource.Play();
    }
}
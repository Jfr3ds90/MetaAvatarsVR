using Fusion;
using TMPro;
using UnityEngine;

public class TimerSync : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private ParticleSystem confettiParticle;

    const int timerDuration = 3;

    [Networked] private TickTimer NetworkedTimer { get; set; }
    [Networked] private NetworkBool IsTimerActive { get; set; }
    [Networked] private float DisplayTime { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            DisplayTime = timerDuration;
        }
        UpdateCounterText();
    }

    public void ToggleTimer()
    {
        if (!Object.HasStateAuthority)
        {
            RPC_ToggleTimer();
            return;
        }

        if (!IsTimerActive)
        {
            if (DisplayTime <= 0 || NetworkedTimer.Expired(Runner))
            {
                DisplayTime = timerDuration;
                NetworkedTimer = TickTimer.CreateFromSeconds(Runner, timerDuration);
            }

            IsTimerActive = true;
        }
        else
        {
            IsTimerActive = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsTimerActive)
        {
            if (!NetworkedTimer.Expired(Runner))
            {
                DisplayTime = (float)NetworkedTimer.RemainingTime(Runner);
            }
            else
            {
                DisplayTime = 0;
                IsTimerActive = false;
                if (confettiParticle != null && Runner.IsForward)
                {
                    RPC_PlayConfetti();
                }
            }
        }
    }

    // Para todos los clientes
    public override void Render()
    {
        UpdateCounterText();
    }

    [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Reliable)]
    private void RPC_ToggleTimer() => ToggleTimer();
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayConfetti()
    {
        if (confettiParticle != null)
        {
            confettiParticle.Play();
        }
    }

    private void UpdateCounterText()
    {
        if (DisplayTime > 0)
        {
            timerText.text = $"{DisplayTime:0.00}";
        }
        else
        {
            timerText.text = "0.00";
        }
    }
}
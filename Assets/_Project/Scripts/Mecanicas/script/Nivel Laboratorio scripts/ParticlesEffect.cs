using UnityEngine;
using HackMonkeys.Debugging;

public class ParticlesEffect : MonoBehaviour
{
    private void OnParticleCollision(GameObject other)
    {
            AdvancedDebugSystem.Log(other.name + " es lo que choco", LogCategory.Avatar, LogLevel.Debug);
    }
    private void OnParticleTrigger()
    {
        AdvancedDebugSystem.Log(name + " es quien detecto", LogCategory.Avatar, LogLevel.Debug);
    }
}

using UnityEngine;

public class ParticlesEffect : MonoBehaviour
{
    private void OnParticleCollision(GameObject other)
    {
            Debug.Log(other.name + " es lo que choco");
    }
    private void OnParticleTrigger()
    {
        Debug.Log(name + " es quien detecto");
    }
}

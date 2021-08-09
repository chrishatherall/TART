using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;

public class FootstepPool : MonoBehaviour
{
    readonly string logSrc = "FootstepPool";

    public PhysicMaterial material;

    public bool defaultPool;

    [SerializeField]
    AudioClip[] footstepSounds;

    public AudioClip GetRandomFootstep()
    {
        if (footstepSounds.Length == 0) return null;
        return footstepSounds[Random.Range(0, footstepSounds.Length)];
    }

    void Start()
    {
        if (!material || footstepSounds.Length == 0) lm.LogError(logSrc, "Missing material/audioClips!");
    }
}

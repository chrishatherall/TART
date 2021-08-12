using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays a sounds intermittently

[RequireComponent(typeof(AudioSource))]
public class IntermittentSound : MonoBehaviour
{
    [SerializeField]
    float TimeBetweenSounds = 1f;
    float timeUntilSound;

    AudioSource AS;

    // Start is called before the first frame update
    void Start()
    {
        timeUntilSound = TimeBetweenSounds;
        AS = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        timeUntilSound -= Time.deltaTime;

        if (timeUntilSound < 0f)
        {
            AS.Play();
            timeUntilSound = TimeBetweenSounds;
        }
    }
}

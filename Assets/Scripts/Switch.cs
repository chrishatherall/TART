using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script runs on all clients, RPC'd by Activatable, and as such needs to be 100% predictable.
public class Switch : MonoBehaviour, IPunObservable
{
    // ref to dynamic transform
    // transform rot/pos when off and on

    [SerializeField]
    bool _state; // true is on, false is off

    [SerializeField]
    GameObject[] controllableObjects;

    // Sound to play when activated
    [SerializeField]
    AudioSource activateSound;

    public bool State { 
        get => _state;
        set {
            // If state changed, set our connected objects
            if (_state != value) SetObjectState(value);
            // Set new state
            _state = value;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // Set all objects to our current state
        SetObjectState(State);
        // Try to find an audio source if one hasn't been set
        if (!activateSound) activateSound = GetComponent<AudioSource>();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        { stream.SendNext(State); }
        else
        { this.State = (bool)stream.ReceiveNext(); }
    }

    // TODO instead of setting object state we use SendMessage with a custom string and this state bool
    // E.g. .SendMessage("Lock", true);

    // Called via an Activatable script when activated
    public void OnActivated(Vector3 position)
    {
        // Play sound
        if (activateSound && activateSound.clip) activateSound.Play();
        // Switch state
        State = !State;
    }

    // Sets the state of all connected objects
    void SetObjectState (bool newState)
    {
        if (controllableObjects.Length > 0)
        {
            foreach (GameObject co in controllableObjects)
            {
                co.SetActive(newState);
            }
        }
    }

}

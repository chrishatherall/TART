using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CollapsingDoor : MonoBehaviourPun, IPunObservable
{
    // X scale when closed
    public float closedScale = 0.1f;

    // X scale when opened
    public float openedScale = 2f;

    // How much scale is added/subtracted per second
    public float movementPerSec = 0.3f;

    // Trying to be opened, false is closed.
    //[SyncVar]
    public bool tryingToOpen = true;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(tryingToOpen);
        }
        else
        {
            // Network player, receive data
            this.tryingToOpen = (bool)stream.ReceiveNext();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Technically this could all be done on the server, but if we do the same changes on the client it'll look more smooth.
        // If we didn't, we'd rely on the scale sync and it'd be jittery.

        // Get current x scale
        float currentXScale = transform.localScale.x;
        if (tryingToOpen)
        {
            // Already open
            if (currentXScale == openedScale) return;
            // Increase scale
            currentXScale += movementPerSec * Time.deltaTime;
            if (currentXScale > openedScale) currentXScale = openedScale;
            transform.localScale = new Vector3(currentXScale, transform.localScale.y, transform.localScale.z);
        }
        else
        {
            // Already closed
            if (currentXScale == closedScale) return;
            // Descrease scale
            currentXScale -= movementPerSec * Time.deltaTime;
            if (currentXScale < closedScale) currentXScale = closedScale;
            transform.localScale = new Vector3(currentXScale, transform.localScale.y, transform.localScale.z);
        }
    }

    void OnActivated ()
    {
        tryingToOpen = !tryingToOpen;
    }
}

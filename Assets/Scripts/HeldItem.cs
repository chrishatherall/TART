using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeldItem : MonoBehaviour, IPunObservable
{
    // Prefab respresenting this item if it were dropped/spawned in the world
    public GameObject worldPrefab;

    // Handy reference to gun script (if this item is one)
    public Gun gun;

    // Item name
    public string nickname;

    public GameObject rightHandIKAnchor;

    public int _ownerPlayerId;
    public int OwnerPlayerId
    {
        get => _ownerPlayerId;
        set
        {
            // If the owner has changed, call our setup method
            if (_ownerPlayerId != value)
            {
                _ownerPlayerId = value;
                this.SendMessage("SetOwner", _ownerPlayerId);
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(OwnerPlayerId);
        }
        else
        {
            // Network player, receive data
            this.OwnerPlayerId = (int)stream.ReceiveNext();
        }
    }

    // Sets values from the fps controller that an item could use. TODO maybe use setters?
    public void SetValues (Vector3 aimOrigin, bool triggerDown)
    {
        if (gun)
        {
            gun.aimOrigin = aimOrigin;
            gun.triggerDown = triggerDown;
        }
    }

    public void Start()
    {
        gun = GetComponent<Gun>();
    }
}

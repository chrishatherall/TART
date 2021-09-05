﻿using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeldItem : MonoBehaviour, IPunInstantiateMagicCallback
{
    // Prefab respresenting this item if it were dropped/spawned in the world
    public GameObject worldPrefab;

    // Handy reference to gun script (if this item is one)
    public Gun gun;

    // Item name
    public string nickname;

    public GameObject rightHandIKAnchor;

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        SendMessage("SetOwner", info.photonView.OwnerActorNr);
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

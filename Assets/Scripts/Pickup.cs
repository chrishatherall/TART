using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;

// A pickup is something that can be E'd by a player and is highlighted when they look at it.
// Examples: Guns, items on floor
public class Pickup : MonoBehaviourPun
{
    // Name of the item, which is shown on the UI
    public string nickname;

    // TODO we'd probably have a slot number here too

    // The prefab representing the held version of this item
    public GameObject prefabHeld;

    public AudioClip pickupSound;

    private void Start()
    {
        // parent this item to the GM's spawned-items object to keep things tidy
        this.transform.parent = gm.itemSpawnParent;
    }
}

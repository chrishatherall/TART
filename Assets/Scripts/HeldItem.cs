using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeldItem : MonoBehaviour
{
    // Prefab respresenting this item if it were dropped/spawned in the world
    public GameObject worldPrefab;

    // Handy reference to gun script (if this item is one)
    public Gun gun;

    // Item name
    public string nickname;

    // Sets values from the fps controller that an item could use. TODO maybe use setters?
    public void SetValues (Vector3 aimOrigin, Camera cam, bool triggerDown, Vector3 recoilAxis)
    {
        if (gun)
        {
            gun.cam = cam;
            gun.aimOrigin = aimOrigin;
            gun.aimVector = cam.transform.forward;
            gun.triggerDown = triggerDown;
            gun.recoilAxis = recoilAxis;
        }
    }

    public void Start()
    {
        gun = GetComponent<Gun>();
    }
}

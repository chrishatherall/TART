using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NO LONGER USED

// Used on objects which require smart interpolation, such as thrown items

[RequireComponent(typeof (PhotonTransformViewClassic))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class SyncTransform : MonoBehaviourPun
{
    PhotonTransformViewClassic ptvc;
    Rigidbody rb;

    private void Start()
    {
        ptvc = GetComponent<PhotonTransformViewClassic>();
        rb = this.GetComponent<Rigidbody>();
    }

    public void Update()
    {
        if (photonView.IsMine) ptvc.SetSynchronizedValues(rb.velocity, 0f);
    }
}

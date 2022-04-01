using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;
using TART;

public class Draggable : MonoBehaviour
{
    readonly string logSrc = "BodyPart";

    public PhotonView pv { get => photonView; }
    private PhotonView photonView;

    public Rigidbody rb { get => rigidBody; }
    private Rigidbody rigidBody;

    private void Start()
    {
        // Try to find a photonview on self or upwards
        photonView = GetComponent<PhotonView>();
        if (!photonView) photonView = GetComponentInParent<PhotonView>();
        // Try to find rigidbody
        rigidBody = GetComponent<Rigidbody>();

        if (!rigidBody)
        {
            lm.Log(logSrc, $"No RigidBody found.");
            this.enabled = false;
            return;
        }

        if (!photonView)
        {
            lm.Log(logSrc, $"No PhotonView found.");
            this.enabled = false;
            return;
        }

        if (photonView.OwnershipTransfer != OwnershipOption.Takeover)
        {
            lm.Log(logSrc, $"PhotonViewPhoton view Ownership Transfer not set to Takeover.");
            this.enabled = false;
            return;
        }
    }
}

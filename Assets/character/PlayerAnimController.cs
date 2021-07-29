using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

// Pulls movement details from the FpsController, syncs those values to the network, and passes them to the animator

public class PlayerAnimController : MonoBehaviourPun, IPunObservable
{
    // Animation parameters
    float frontBackMovement; // 1 = full forwards, -1 = full backwards
    float leftRightMovement; // 1 = full right, -1 = full left
    bool isMoving;           // false if idle, true if moving
    bool isGrounded;         // true if on floor
    
    // Component references
    FpsController fpsController;
    Animator animator;

    public void Start()
    {
        fpsController = GetComponent<FpsController>();
        animator = GetComponent<Animator>();

        if (!fpsController || !animator)
        {
            // Turn this component off if we can't find the required components
            gm.LogError("PlayerAnimController could not find required components");
            this.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (photonView.IsMine)
        {
            // Pull details from the FpsController
            frontBackMovement = fpsController.frontBackMovement;
            leftRightMovement = fpsController.leftRightMovement;
            isMoving = fpsController.isMoving;
            isGrounded = fpsController.isGrounded;

            // TODO this should be pulled from the FpsController instead of directly
            if (Input.GetKeyDown("space"))
            {
                animator.SetTrigger("triggerJumped");
            }
        }

        // Set details on animator
        animator.SetFloat("frontBackMovement", frontBackMovement);
        animator.SetFloat("leftRightMovement", leftRightMovement);
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isGrounded", isGrounded);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // TODO not syncing jump
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(frontBackMovement);
            stream.SendNext(leftRightMovement);
            stream.SendNext(isMoving);
            stream.SendNext(isGrounded);
        }
        else
        {
            // Network player, receive data
            this.frontBackMovement = (float)stream.ReceiveNext();
            this.leftRightMovement = (float)stream.ReceiveNext();
            this.isMoving = (bool)stream.ReceiveNext();
            this.isGrounded = (bool)stream.ReceiveNext();
        }
    }
}

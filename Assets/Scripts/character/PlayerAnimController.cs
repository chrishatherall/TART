using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;

// Pulls movement details from the FpsController, syncs those values to the network, and passes them to the animator

public class PlayerAnimController : MonoBehaviourPun, IPunObservable
{
    readonly string logSrc = "PLAYER_ANIM";

    // IK
    [SerializeField]
    Vector3 lookPos;
    [SerializeField]
    GameObject rightHandIKObj;

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

        if (!animator)
        {
            // Turn this component off if we can't find the required components
            lm.LogError(logSrc,"Could not find required components");
            this.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (photonView.IsMine && fpsController)
        {
            // Pull details from the FpsController
            frontBackMovement = fpsController.frontBackMovement;
            leftRightMovement = fpsController.leftRightMovement;
            isMoving = fpsController.isMoving;
            isGrounded = fpsController.isGrounded;
            lookPos = fpsController.lastHit.point;

            // TODO this should be pulled from the FpsController instead of directly
            if (Input.GetKeyDown("space"))
            {
                animator.SetTrigger("triggerJumped");
            }

            if (fpsController.player.heldItemScript)
            {
                rightHandIKObj = fpsController.player.heldItemScript.rightHandIKAnchor;
            } else
            {
                rightHandIKObj = null;
            }
        }

        // Set details on animator
        animator.SetFloat("frontBackMovement", frontBackMovement);
        animator.SetFloat("leftRightMovement", leftRightMovement);
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isGrounded", isGrounded);

    }

    // Callback for calculating IK
    void OnAnimatorIK()
    {
        if (!animator) return;

        //Head IK
        if (lookPos != null)
        {
            animator.SetLookAtWeight(1);
            animator.SetLookAtPosition(lookPos);
        }
        else
        {
            animator.SetLookAtWeight(0);
        }

        // Held object IK
        if (rightHandIKObj)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandIKObj.transform.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandIKObj.transform.rotation);
        } 
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
        }
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

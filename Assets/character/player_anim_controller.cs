using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

// Pulls movement details from the fps_controller, syncs those values to the network, and passes them to the animator

public class player_anim_controller : MonoBehaviourPun, IPunObservable
{
    // Animation parameters
    float frontBackMovement; // 1 = full forwards, -1 = full backwards
    float leftRightMovement; // 1 = full right, -1 = full left
    bool isMoving;           // false if idle, true if moving
    bool isGrounded;         // true if on floor

    // Component refs
    fps_controller fpsController;
    Animator animator;


    public void Start()
    {
        fpsController = GetComponent<fps_controller>();
        animator = GetComponent<Animator>();

        if (!fpsController || !animator)
        {
            gm.LogError("player_anim_controler could not find required components");
            this.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (photonView.IsMine)
        {
            // TODO pull some details like isDead from player

            // Pull details from the fps_controller
            frontBackMovement = fpsController.frontBackMovement;
            leftRightMovement = fpsController.leftRightMovement;
            isMoving = fpsController.isMoving;
            isGrounded = fpsController.isGrounded;


            // TODO remove this immediately it is filthy
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

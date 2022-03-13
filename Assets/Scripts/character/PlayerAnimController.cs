using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;

// Pulls movement details from the FpsController, syncs those values to the network, and passes them to the animator

public class PlayerAnimController : MonoBehaviour
{
    readonly string logSrc = "PLAYER_ANIM";

    // IK
    [SerializeField]
    Vector3 lookPos;
    [SerializeField]
    GameObject rightHandIKObj;

    [SerializeField]
    GameObject bHead;
    [SerializeField]
    GameObject bUpperChest;
    [SerializeField]
    GameObject bChest;
    [SerializeField]
    GameObject bSpine;
    [SerializeField]
    GameObject bHips;
    [SerializeField]
    GameObject root;

    Quaternion defbUpperChest;
    Quaternion defbChest;
    Quaternion defbSpine;
    //Quaternion defbHips;
    [SerializeField]
    Vector3 defbHips = new Vector3(0f, 0f, -90f);

    // The parent of the item anchor which rotates to look at our hit point. Makes guns aim
    // roughly at the place we're looking
    [SerializeField]
    GameObject itemAnchorParent;
  
    // Component references
    Animator animator;
    Player p;

    public void Start()
    {
        if (bUpperChest) defbUpperChest = bUpperChest.transform.localRotation;
        if (bChest) defbChest = bChest.transform.localRotation;
        if (bSpine) defbSpine = bSpine.transform.localRotation;

        animator = GetComponent<Animator>();
        p = GetComponent<Player>();

        if (!animator || !p)
        {
            // Turn this component off if we can't find the required components
            lm.LogError(logSrc,"Could not find required components");
            this.enabled = false;
        }

        // Set jump trigger on Player event
        p.OnJump += () => animator.SetTrigger("triggerJumped");
    }

    // Update is called once per frame
    void Update()
    {

        // Set our IK targets
        if (p.heldItemScript)
        {
            rightHandIKObj = p.heldItemScript.rightHandIKAnchor;
        } else
        {
            rightHandIKObj = null;
        }

        // Set details on animator
        animator.SetFloat("frontBackMovement", p.frontBackMovement);
        animator.SetFloat("leftRightMovement", p.leftRightMovement);
        animator.SetBool("isMoving", p.isMoving);
        animator.SetBool("isGrounded", p.IsGrounded);
        animator.SetBool("isCrouching", p.IsCrouching);

        // Rotate item anchor
        itemAnchorParent.transform.LookAt(p.aim);

    }

    // Callback for calculating IK (called by Animator)
    void OnAnimatorIK()
    {
        if (!animator) return;

        // Head IK
        if (lookPos != null)
        {
            animator.SetLookAtWeight(1);
            animator.SetLookAtPosition(p.aim);
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

        // Override a load of upper body values so when running we don't lean forward loads, which looks awful in first-person
        if (bUpperChest) animator.SetBoneLocalRotation(HumanBodyBones.UpperChest, defbUpperChest);
        if (bChest) animator.SetBoneLocalRotation(HumanBodyBones.Chest, defbChest);
        if (bSpine) animator.SetBoneLocalRotation(HumanBodyBones.Spine, defbSpine);
        animator.SetBoneLocalRotation(HumanBodyBones.Hips, Quaternion.Euler(defbHips));
    }

    void LateUpdate()
    {
        // we don't apply root motion, so we need to bump the model up
        root.transform.localPosition = new Vector3(0f, p.IsCrouching ? 0.45f : 0.66f, 0f);
    }

    public void SetHead(bool headOn)
    {
        // Sets the head on or off
        bHead.transform.localScale = headOn ?
            new Vector3(1f, 1f, 1f) :
            new Vector3(0f, 0f, 0f);
    }
}

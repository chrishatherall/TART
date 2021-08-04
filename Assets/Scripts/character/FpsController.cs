﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;
using static LogManager;

// TODO in an ideal world this controller would be based on a rigidbody to allow more detailed movement
// as the basic CharacterController can't handle acceleration changes, and won't be affected by
// environmental affects.

// TODO also this script should only do control, it should not care about player state. Consider
// moving item drops, activation, etc, to the player controller

public class FpsController : MonoBehaviourPun
{
    readonly string logSrc = "FPS_CTRL";

    #region Camera Movement Variables
    // Ref to camera component
    public Camera playerCamera;
    // Public variables
    public float fov = 60f;
    public bool invertCamera = false;
    public bool cameraCanMove = true;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 50f;
    // Internal Variables - these could be modified by guns for recoil
    private float yaw = 0.0f;
    private float pitch = 0.0f;
    // Camera wiggle. Every time this is set, the rotation of the CameraWiggler object is randomised (to this scale).
    // Eg, a C4 knock will set this very high once. Guns will set this low quite often.
    // This is reduced over time by a set amount.
    private float _camWiggle; // Effectively degrees of rotation for x/y.
    [SerializeField]
    float CamWiggleReductionPerSecond = 6f;
    // Ref to the wiggle object, parent of our camera
    [SerializeField]
    GameObject CamWiggleObject;
    // Ref to our rigidBody dragger
    [SerializeField]
    GameObject rbDragger;
    FixedJoint fj;
    #endregion

    // Character move speed.
    [SerializeField]
    float speed = 6.0f;
    // Strength of gravity
    [SerializeField]
    float gravity = -10f;
    // Speed at which the character is falling.
    [SerializeField]
    float fallingSpeed = 0f;
    // Strength of a jump.
    [SerializeField]
    float jumpStrength = 6f;
    // Is character currently crouching
    bool isCrouching = false;

    // Create a layermask which ignores layer 7, so we dont constantly activate ourselves
    int layermask = ~(1 << 7);
    // Range at which we can activate buttons/doors
    [SerializeField]
    float activateRange = 2f;
    // Reference to our camera
    private Camera cam;
    float camHeight;
    // Reference to our player script
    public Player player;

    // The last raycast hit of our camera
    public RaycastHit lastHit;

    // Movement values for animation controller
    public bool isGrounded;
    public float frontBackMovement;
    public float leftRightMovement;
    public bool isMoving;

    // Ref to the character controller.
    CharacterController charCon;
    float ccHeight;
    // Ref to the player inventory
    Inventory inventory;
    // Ref to the top of our head, for determining if we can uncrouch
    [SerializeField]
    GameObject topOfHead;
    // The audio source we use to emit sounds from this character
    private AudioSource audioSource;

    // The parent of the item anchor which rotates to look at our hit point. Makes guns aim
    // roughly at the place we're looking
    [SerializeField]
    GameObject itemAnchorParent;

    // The text box shown below our cursor, for displaying information on pickups, activatables, etc
    public UnityEngine.UI.Text cursorTooltip;

    public float CameraWiggle { 
        get => _camWiggle; 
        set 
        {
            // Ignore lower values.
            if (_camWiggle > value) return;
            // Do not allow value to be absurd.
            _camWiggle = Mathf.Clamp(value, 0f, 10f);
            // Set rotation of cam wiggle object
            Vector3 curRot = CamWiggleObject.transform.localEulerAngles;
            float y = curRot.y + _camWiggle * Random.Range(-0.25f, 0.25f); // Y wiggle (left/right) can go either way.
            float x = curRot.x + _camWiggle * Random.Range(0f, -1f); // X wiggle (up/down) only goes up. 
            Quaternion newRot = Quaternion.Euler(x, y, 0f);
            CamWiggleObject.transform.localRotation = newRot;
        }
        
    }

    // Use this for initialization
    void Awake()
    {
        // Find player script
        player = GetComponent<Player>();
        // Find character controller
        charCon = GetComponent<CharacterController>();
        // Find the player inventory
        inventory = GetComponent<Inventory>();
        // Find camera
        cam = GetComponentInChildren<Camera>();
        // Find local audio source, used for pickup sounds
        audioSource = GetComponent<AudioSource>();
        // Set rb dragger stuff
        fj = rbDragger.GetComponent<FixedJoint>();

        if (!player || !charCon || !inventory)
        {
            lm.LogError(logSrc,"Missing components!");
        }

        // Turn off the cursor
        Cursor.lockState = CursorLockMode.Locked;

        ccHeight = charCon.height;
        camHeight = cam.transform.localPosition.y;
    }

    // Update is called once per frame
    void Update()
    {
        if (player.isDead) return; // Don't do anything if we're dead. Introduces a bug when dying in midair, but whatever

        #region Camera
        // Control camera movement
        yaw = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivity;
        // Support dumb inverted camera
        if (!invertCamera)
        {
            pitch -= mouseSensitivity * Input.GetAxis("Mouse Y");
        }
        else
        {
            // Inverted Y
            pitch += mouseSensitivity * Input.GetAxis("Mouse Y");
        }
        // Clamp pitch between lookAngle
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        // Set y rotation of player object
        transform.localEulerAngles = new Vector3(0, yaw, 0);
        // Set x rotation of camera object
        playerCamera.transform.localEulerAngles = new Vector3(pitch, 0, 0);

        // Reduce camera wiggle
        // BIG NOTE: This is about camera wiggle, NOT gun recoil. Gun recoil uses this as a base, but has its own firing inaccuracy.
        // The reduction amount should be fraction of the x rotation, or CamWiggleReductionPerSecond, whichever is higher.
        float extraReduction = CamWiggleObject.transform.localEulerAngles.x;
        if (extraReduction > 180) extraReduction = 360 - extraReduction; // Normalise reduction that goes under 0.
        float reduction = CamWiggleReductionPerSecond + extraReduction * extraReduction; // Increase with a square, in case we get hit with something massive?
        // Rotate back toward 0
        Quaternion rotAmount = Quaternion.RotateTowards(CamWiggleObject.transform.localRotation, Quaternion.identity, reduction * Time.deltaTime);
        // Reduce wiggle value
        _camWiggle = Mathf.Clamp(_camWiggle - reduction * Time.deltaTime, 0f, 100f);
        CamWiggleObject.transform.localRotation = rotAmount;
        #endregion

        #region Crouching
        if (!isCrouching && Input.GetKey(KeyCode.LeftControl))
        {
            isCrouching = true;
            charCon.height = ccHeight / 2;
            charCon.center = new Vector3(0f, ccHeight / 2, 0f);
            topOfHead.transform.localPosition = new Vector3(0f, charCon.height, 0f);
            cam.transform.localPosition = new Vector3(0f, camHeight / 2, cam.transform.localPosition.z);
        }
        if (isCrouching && !Input.GetKey(KeyCode.LeftControl))
        {
            // check to see if we can uncrouch, by casting a ray up and seeing if it's clear
            RaycastHit crouchHit;
            bool canUncrouch = !Physics.Raycast(topOfHead.transform.position, Vector3.up, out crouchHit, ccHeight/2);//, layermask);
            if (canUncrouch)
            {
                isCrouching = false;
                charCon.center = new Vector3(0f, ccHeight / 2, 0f);
                charCon.height = ccHeight;
                topOfHead.transform.localPosition = new Vector3(0f, charCon.height, 0f);
                cam.transform.localPosition = new Vector3(0f, camHeight, cam.transform.localPosition.z);
            }
        }
        #endregion

        #region Movement
        // Clean up tooltip text
        cursorTooltip.text = "";

        // Set grounded flag for the animation controller
        isGrounded = charCon.isGrounded;

        // Get input values 
        frontBackMovement = Input.GetAxis("Vertical");
        leftRightMovement = Input.GetAxis("Horizontal");
        isMoving = frontBackMovement != 0 || leftRightMovement != 0; // We're moving if there is any input  TODO maybe add jump

        // Get forward/strafe direction
        Vector3 strafe = leftRightMovement * transform.right;
        Vector3 forward = frontBackMovement * transform.forward;
        Vector3 moveDirection = forward + strafe;
        moveDirection *= speed;

        if (player.isDead) // Don't allow movement input if dead, by overwriting input
        {
            moveDirection = new Vector3();
        }

        if (charCon.isGrounded)
        {
            fallingSpeed = -0.3f; // When on the ground we set a little downward speed otherwise the controller seems 
            // to 'bounce' on the ground and half the time doesn't consider itself grounded.
            // Go up if we're hitting jump.
            if (!player.isDead && Input.GetKeyDown("space"))
            {
                fallingSpeed = jumpStrength;
            }
        }
        else
        {
            // Increase gravity if we're in the air
            fallingSpeed += gravity * Time.deltaTime;
        }

        // Apply falling speed to move direction
        moveDirection.y = fallingSpeed;

        // Instruct the controller to move us
        charCon.Move(moveDirection * Time.deltaTime);

        #endregion

        #region Key-specific events
        // Throw objects
        if (Input.GetKeyDown("g") && player.heldItem)
        {
            TryDropHeldItem();
        }

        // Reload
        if (Input.GetKeyDown("r") && player.heldItem)
        {
            player.heldItem.SendMessage("Reload");
        }
        #endregion

        #region Interaction



        // Raycast forward from our camera to see if we're looking at anything important within range.
        if (!cam) cam = GetComponentInChildren<Camera>();
        Vector3 rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
        bool hitSomething = Physics.Raycast(rayOrigin, cam.transform.forward, out RaycastHit hit, 500f, layermask);
        if (hitSomething)
        {
            // Set our last hit to this point
            lastHit = hit;

            // We can't pickup or activate anything beyond our range.
            if (Vector3.Distance(rayOrigin, hit.point) < activateRange)
            {
                // Find a pickup
                Pickup pickup = lastHit.transform.GetComponent<Pickup>();
                if (pickup && pickup.enabled == true)
                {
                    // Set our ui tooltip
                    cursorTooltip.text = "[E] Pick up " + pickup.nickname;

                    // Try to pick up items
                    if (Input.GetKeyDown(KeyCode.E)) TryPickupItem(pickup.gameObject);
                }

                // Find an activatable
                Activatable act = lastHit.transform.GetComponent<Activatable>();
                if (act)
                {
                    // Set our ui tooltip
                    cursorTooltip.text = "[E] Activate " + act.nickname;

                    // Try to activate objects
                    if (Input.GetKeyDown(KeyCode.E)) TryToActivate(lastHit.transform.gameObject, lastHit.point);
                }

            }

            // Rotate item anchor
            itemAnchorParent.transform.LookAt(hit.point);

        } 
        else 
        {
            // We're not looking at anything in range, so clear the UI tooltip
            cursorTooltip.text = "";
        }

        // If we pressed F and are dragging a body, stop
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (fj.connectedBody)
            {
                fj.connectedBody = null;
            } else if (hitSomething)
            {
                // Try to drag
                Rigidbody rb = lastHit.transform.GetComponent<Rigidbody>();
                if (rb)
                {
                    rbDragger.transform.position = lastHit.transform.position;
                    fj.connectedBody = rb;
                }
            }
        }

        #endregion

        #region Misc
        // Tell held item about some stuff
        if (player.heldItemScript)
        {
            player.heldItemScript.SetValues(rayOrigin, Input.GetButton("Fire1"));
        }
        #endregion
    }

    void TryPickupItem (GameObject item)
    {
        // If the player is already holding an item, drop it before picking up the new one
        if (player.heldItem)
        {
            // TODO needs to be run on server?? Or we could run it here and set ownership to the scene
            TryDropHeldItem();
        }

        // Find the PhotonView of the item to be picked up
        PhotonView PV = item.GetComponent<PhotonView>();
        // Find pickup script
        Pickup pickup = item.GetComponent<Pickup>();

        if (!item || !PV || !pickup)
        {
            lm.LogError(logSrc,$"player {player.ID} tried to pick up missing item");
            return;
        }
        lm.Log(logSrc,$"player {player.ID} is picking up {pickup.nickname}");

        // Play sound if the pickup has one
        if (pickup.pickupSound)
        {
            audioSource.clip = pickup.pickupSound;
            audioSource.Play();
        }

        // Check to see if we're picking up a non-held item like a key
        if (!pickup.prefabHeld)
        {
            gm.Alert("Picked up " + pickup.nickname);
            inventory.AddItem(pickup);
            Destroy(item); // Note, this only destroys the item for us
            return;
        }

        // Create new item in our hands
        GameObject newItem = PhotonNetwork.Instantiate(pickup.prefabHeld.name, player.itemAnchor.transform.position, Quaternion.identity);

        // Server should destroy the original
        GameManager.gm.photonView.RPC("DestroyItem", RpcTarget.MasterClient, PV.ViewID);

        // Setup the gun and tell other players to
        // TODO assumes it's a gun
        // TODO items with an audio source should be set to 2d spatial blend so the sound doesn't favour one speaker (annoying)
        newItem.GetComponent<Gun>().Setup(player.ID);
        newItem.GetPhotonView().RPC("Setup", RpcTarget.Others, player.ID);

        newItem.layer = 7;

    }

    public void Reset()
    {
        // Turn off the character controller before force-moving, or it'll just set us right back.
        this.charCon.enabled = false;
        this.transform.position = gm.GetPlayerSpawnLocation();
        this.charCon.enabled = true;
    }

    // Drops our held item into the world. Called when we press G or on death
    public void TryDropHeldItem ()
    {
        if (!player.heldItem) return;
        TryDropItem(player.heldItemScript.worldPrefab.name);
        // Destroy the item in our hands.
        PhotonNetwork.Destroy(player.heldItem);
    }

    // Drops an item into the world
    void TryDropItem (string prefabName)
    {
        // Tell server we're dropping our held item.
        photonView.RPC("RpcDropItem", RpcTarget.MasterClient, player.heldItemScript.worldPrefab.name);
    }

    [PunRPC]
    void RpcDropItem (string prefabName)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        lm.Log(logSrc,$"Player {player.ID} dropping item {prefabName}");
        // Create item being dropped
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefabName, cam.transform.position, transform.rotation);
        // Add some force so it moves away from the player who dropped it
        go.GetComponent<Rigidbody>().AddForce(cam.transform.forward * 1000);
    }

    /// <summary>
    /// Attempts to activate an object by sending an activation request to the server.
    /// </summary>
    /// <param name="go">Object to be activated</param>
    /// <param name="position">Position at which the activation occurred, usually a raycast hit</param>
    void TryToActivate(GameObject go, Vector3 position)
    {
        if (player.isDead) return;
        // Find an activatable
        Activatable act = go.GetComponent<Activatable>();
        if (!act) return;
        // Check we have the right key if it's required
        if (act.requiredKey != "" && !inventory.HasItem(act.requiredKey))
        {
            // ui alert
            gm.Alert("NEED " + act.requiredKey);
            lm.Log(logSrc,"NEED " + act.requiredKey);
            return;
        }
        act.Activate(position);
        //PhotonView PV = go.GetComponent<PhotonView>();
        //if (PV) // If the activatable has a photonview, send out a message saying we're activating it
        //{
        //    PV.RPC("Activate", RpcTarget.All, position); // Send to all to other clients can make button noises and such
        //}
        //else // If no photonview, it's a local-only activatable
        //{
        //    act.Activate(position);
        //}
    }

}
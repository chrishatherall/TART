﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// TODO in an ideal world this controller would be based on a rigidbody to allow more detailed movement
// as the basic CharacterController can't handle acceleration changes, and won't be affected by
// environmental affects.

public class fps_controller : MonoBehaviourPun
{

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
    #endregion

    // Character move speed.
    public float speed = 6.0f;
    // Strangth of gravity
    public float gravity = -20f;
    // Speed at which the character is falling.
    private float fallingSpeed = 0f;
    // Strength of a jump.
    public float jumpStrength = 6f;
    // Is character currently crouching
    bool isCrouching = false;

    // Create a layermask which ignores layer 7, so we dont constantly activate ourselves
    int layermask = ~(1 << 7);
    // Range at which we can activate buttons/doors
    public float activateRange = 2f;
    // Reference to our camera
    private Camera cam;
    float camHeight;
    // Reference to our player script
    public Player player;

    // Movement values
    public bool grounded;
    public float frontBackMovement; // For anim controller
    public float leftRightMovement; // For anim controller
    public bool isMoving;          // For anim controller

    // Ref to the character controller.
    CharacterController charCon;
    float ccHeight;
    // Ref to the player inventory
    Inventory inventory;
    // Ref to the top of our head, for determining if we can uncrouch
    public GameObject topOfHead;

    private AudioSource audioSource; 

    // The text box shown below our cursor, for displaying information on pickups, activatables, etc
    public UnityEngine.UI.Text cursorTooltip;

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

        if (!player || !charCon || !inventory)
        {
            Debug.LogError("[fps_controller] Missing components!");
        }

        // turn off the cursor
        Cursor.lockState = CursorLockMode.Locked;

        ccHeight = charCon.height;
        camHeight = cam.transform.localPosition.y;
    }

    // Update is called once per frame
    void Update()
    {
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

        // Clean up tooltip text
        cursorTooltip.text = "";

        // just for debugging
        grounded = charCon.isGrounded;

        // TODO allow less control in the air

        // Get input values 
        frontBackMovement = Input.GetAxis("Vertical");
        leftRightMovement = Input.GetAxis("Horizontal");
        isMoving = frontBackMovement != 0 || leftRightMovement != 0; // We're moving if there is any input  TODO maybe add jump

        // Get forward/strafe direction
        Vector3 strafe = leftRightMovement * transform.right;
        Vector3 forward = frontBackMovement * transform.forward;
        Vector3 moveDirection = forward + strafe;

        if (player.isDead) // Don't allow movement if dead, by overwriting input
        {
            moveDirection = new Vector3();
        }

        if (charCon.isGrounded)
        {
            // Slowest gravity if on the ground.
            //fallingSpeed = gravity * Time.deltaTime;
            // Go up if we're hitting jump
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

        // Apply gravity
        moveDirection.y += fallingSpeed;

        // Instruct the controller to move us
        charCon.Move(moveDirection * Time.deltaTime * speed);


        if (Input.GetKeyDown("escape"))
        {
            // turn on the cursor
            Cursor.lockState = CursorLockMode.None;
        }
        // Dirty mouse catch
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
        }


        // Throw objects
        if (Input.GetKeyDown("g") && player.heldItem)
        {
            TryDropHeldItem();
        }

        // Highlight objects we're targeting
        RaycastHit hit;
        // Check if our raycast has hit anything
        if (!cam) cam = GetComponentInChildren<Camera>();
        Vector3 rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
        

        bool hitSomething = Physics.Raycast(rayOrigin, cam.transform.forward, out hit, activateRange, layermask);
        if (hitSomething)
        {

            // Find a pickup
            Pickup pickup = hit.transform.GetComponent<Pickup>();
            if (pickup != null && pickup.enabled == true)
            {
                // Set our ui tooltip
                cursorTooltip.text = "[E] Pick up " + pickup.nickname;

                // Pick up item on E
                // TODO none of this is sync'd
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryPickupItem(pickup.gameObject);
/*                  // Set the pickup's parent to our gun anchor
                    hit.transform.parent = player.itemAnchor.transform;
                    // Turn off gravity for that object
                    hit.transform.GetComponent<Rigidbody>().useGravity = false;
                    hit.transform.GetComponent<BoxCollider>().enabled = false;
                    // Reset the position/rotation of the pickup
                    hit.transform.localPosition = new Vector3(0f, 0f, 0f);
                    hit.transform.localRotation = new Quaternion(0f, 0f, 0f, 0f);
                    // Disable the pickup component so it can't be grabbed from our hands
                    pickup.enabled = false;
                    // Tell our player script that we're holding something
                    player.heldItem = hit.transform.gameObject;*/
                }

            }

           

            // TODO make right-click hold the mac sideways like a gangsta

            // Find an activatable
            Activatable act = hit.transform.GetComponent<Activatable>();

            if (act != null)
            {
                // Set our ui tooltip
                cursorTooltip.text = "[E] Activate " + act.nickname;

                // Activate objects
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryToActivate(hit.transform.gameObject, hit.point);
                }
            } 


        } 
        else 
        {
            // Remove ui tooltip
            // TODO this is not a perfect thing, kinda buggy
            cursorTooltip.text = "";
        }


        // Tell held item about some stuff
        if (player.heldItemScript)
        {
            player.heldItemScript.SetValues(rayOrigin, cam, Input.GetButton("Fire1"), cam.transform.right);
        }

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

        if (!item || !PV || !item.GetComponent<Pickup>())
        {
            Debug.LogError($"[fps_controller] player {player.id} tried to pick up missing item");
            return;
        }
        Debug.Log($"[fps_controller] player {player.id} is picking up {item.GetComponent<Pickup>().nickname}");

        // Delete old held item
        //if (player.heldItem) Destroy(player.heldItem);
        //player.heldItem = Instantiate(item.GetComponent<Pickup>().prefabHeld, player.itemAnchor.transform);
        //NetworkServer.Spawn(player.heldItem, player.gameObject);
        //player.heldItem.GetComponent<NetworkIdentity>().AssignClientAuthority(player.GetComponent<NetworkIdentity>().connectionToClient);
        //player.heldItemScript = player.heldItem.GetComponent<HeldItem>();

        // Find pickup script
        Pickup pickup = item.GetComponent<Pickup>();

        // Play sound if the pickup has one
        if (pickup.pickupSound)
        {
            audioSource.clip = pickup.pickupSound;
            audioSource.Play();
        }

        // TEMP
        // Do a check to see if we're picking up a non-held item like a keycard
        if (!pickup.prefabHeld)
        {
            GameManager.gm.Alert("Picked up " + pickup.nickname);
            inventory.AddItem(pickup.nickname);
            Destroy(item);
            return;
        }

        //GameObject newItem = Instantiate(pickup.prefabHeld, player.itemAnchor.transform); // may need to set pos/rot
        GameObject newItem = PhotonNetwork.Instantiate(pickup.prefabHeld.name, player.itemAnchor.transform.position, Quaternion.Euler(0f,0f,0f)); // rotation?!

        // Server should destroy the original
        GameManager.gm.photonView.RPC("DestroyItem", RpcTarget.MasterClient, PV.ViewID);

        // Setup the gun and tell others to
        newItem.GetComponent<Gun>().Setup(player.id);
        newItem.GetPhotonView().RPC("Setup", RpcTarget.Others, player.id);

        // Destroy the original pickup
        // TODO not 100% about it being done here but if it's done immediately on the server then the server client
        // can't create the held item.
        //Destroy(item);
    }

    void TryDropHeldItem ()
    {
        if (player.heldItem)
        {
            // Tell everyone we're dropping this item
            photonView.RPC("RpcDropItem", RpcTarget.All, player.heldItemScript.worldPrefab.name, true);
        }
    }

    [PunRPC]
    void RpcDropItem (string prefabName, bool destroyHeldItem)
    {
        Debug.Log($"Player {player.nickname} dropping item {prefabName}");

        if (destroyHeldItem) Destroy(player.heldItem);

        // Only server should create new item
        if (!PhotonNetwork.IsMasterClient) return;
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefabName, cam.transform.position, transform.rotation);
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
        // Find an activatable and make sure it has a photonview
        Activatable act = go.GetComponent<Activatable>();
        if (act == null) return;
        // Check we have the right key if it's required
        if (act.requiredKey != "" && !inventory.HasItem(act.requiredKey))
        {
            // ui alert
            GameManager.gm.Alert("NEED " + act.requiredKey);
            Debug.Log("NEED " + act.requiredKey);
            return;
        }
        PhotonView PV = go.GetComponent<PhotonView>();
        if (PV) PV.RPC("Activate", RpcTarget.All, position); // Send to all to other clients can make button noises and such
        else act.Activate(position); // Local-only activatables
    }
}

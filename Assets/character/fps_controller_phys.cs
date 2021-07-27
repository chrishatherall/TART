using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// TODO in an ideal world this controller would be based on a rigidbody to allow more detailed movement
// as the basic CharacterController can't handle acceleration changes, and won't be affected by
// environmental affects.

public class fps_controller_phys : MonoBehaviourPun
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
    public float maxVelocityChange = 10f;
    // Is character currently crouching
    bool isCrouching = false;
    // Jumping values
    public bool enableJump = true;
    public float jumpPower = 50f;

    // Values for the animation controller. TODO should be read-only
    public bool isGrounded;
    public float frontBackMovement;
    public float leftRightMovement;
    public bool isMoving;

    // Create a layermask which ignores layer 7, so we dont constantly activate ourselves
    int layermask = ~(1 << 7);
    // Range at which we can activate buttons/doors
    public float activateRange = 2f;

    // Reference to our camera
    private Camera cam;
    // Save the original cam height when crouching so we can reset it later
    float camHeight;    
    // Save the original collider height when crouching so we can reset it later
    float ccHeight;
    // Reference to our player script
    public Player player;
    // Ref to the player inventory
    Inventory inventory;
    // Ref to the top of our head, used to determine if we can uncrouch by casting a ray upwards
    public GameObject topOfHead;
    // Ref to the audio source for playing sounds locally
    private AudioSource audioSource; 
    // The text box shown below our cursor, for displaying information on pickups, activatables, etc
    public UnityEngine.UI.Text cursorTooltip;
    // Ref to the rigidbody
    Rigidbody rb;
    // Ref to collider
    CapsuleCollider cc;

    // Use this for initialization
    void Awake()
    {
        // Find required components
        player = GetComponent<Player>();
        inventory = GetComponent<Inventory>();
        cam = GetComponentInChildren<Camera>();
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CapsuleCollider>();

        if (!player || !inventory || !cam || !audioSource || !rb)
        {
            Debug.LogError("[fps_controller] Missing components!");
        }

        // turn off the cursor
        Cursor.lockState = CursorLockMode.Locked;

        camHeight = cam.transform.localPosition.y;
        ccHeight = cc.height;
    }

    bool PlayerCanMove()
    {
        return !player.isDead;
    }

    bool CheckGround()
    {
        float distance = .20f;
        return Physics.Raycast(transform.position + new Vector3(0f, 0.01f, 0f), Vector3.down, out _, distance, layermask);
    }

    private void Jump()
    {
        // Adds force to the player rigidbody to jump
        if (isGrounded)
        {
            rb.AddForce(0f, jumpPower, 0f, ForceMode.Impulse);
            isGrounded = false; // Stops instant double-jump
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Clean up tooltip text
        cursorTooltip.text = "";

        isGrounded = CheckGround();

        #region CameraMovement
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
            cc.height = ccHeight / 2;
            cc.center = new Vector3(0f, ccHeight / 2, 0f);
            topOfHead.transform.localPosition = new Vector3(0f, cc.height, 0f);
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
                cc.center = new Vector3(0f, ccHeight / 2, 0f);
                cc.height = ccHeight;
                topOfHead.transform.localPosition = new Vector3(0f, cc.height, 0f);
                cam.transform.localPosition = new Vector3(0f, camHeight, cam.transform.localPosition.z);
            }
        }
        #endregion

        if (Input.GetKeyDown(KeyCode.Space)) Jump();

        #region UI
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

        #endregion

        #region Interaction

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
        #endregion


        // Tell held item about some stuff
        if (player.heldItemScript)
        {
            player.heldItemScript.SetValues(rayOrigin, Input.GetButton("Fire1"));
        }

    }

    void FixedUpdate()
    {
        #region Movement

        if (PlayerCanMove())
        {
            // Calculate how fast we should be moving
            Vector3 targetVelocity = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            targetVelocity = transform.TransformDirection(targetVelocity) * speed;

            // Air movement is only a fraction of speed
            float adjustedMVC = maxVelocityChange;
            if (!isGrounded) adjustedMVC *= 0.1f; 

            // Apply a force that attempts to reach our target velocity
            Vector3 velocity = rb.velocity;
            Vector3 velocityChange = (targetVelocity - velocity);
            velocityChange.x = Mathf.Clamp(velocityChange.x, -adjustedMVC, adjustedMVC);
            velocityChange.z = Mathf.Clamp(velocityChange.z, -adjustedMVC, adjustedMVC);
            velocityChange.y = 0;

            //rb.AddForce(velocityChange, ForceMode.VelocityChange);
            rb.MovePosition(rb.position + targetVelocity * Time.fixedDeltaTime); // TODO both cause jittering
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

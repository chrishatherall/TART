using System.Collections;
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
    public Camera cam;
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
    // last input, used when in the air
    Vector3 lastInput = Vector3.zero;

    // Create a layermask which ignores layer 7, so we dont constantly activate ourselves
    int layermask = ~(1 << 7);
    // Range at which we can activate buttons/doors
    [SerializeField]
    float activateRange = 2f;
    // The difference between our head height and our cam height
    float camHeadHeightDiff;
    // The target camera position for our cam lerper. Adjusted when crouching
    Vector3 camTargetPoint;
    float camLerpSmoothTime = 0.1f;
    Vector3 camLerpVelocity = Vector3.zero;
    // Reference to our player script
    public Player p;

    // The last raycast hit of our camera
    public RaycastHit lastHit;
    
    // Ref to the character controller.
    CharacterController charCon;
    float ccHeight;
    // Ref to the player inventory
    Inventory inventory;
    // The audio source we use to emit sounds from this character
    private AudioSource audioSource;

    [SerializeField]
    AudioClip cannotPlaceClip;

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
        p = GetComponent<Player>();
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

        if (!p || !charCon || !inventory || !cam || !CamWiggleObject)
        {
            lm.LogError(logSrc,"Missing components!");
        }

        // Turn on character controller
        if (photonView.IsMine) charCon.enabled = true;

        // Turn off the cursor
        Cursor.lockState = CursorLockMode.Locked;

        ccHeight = charCon.height;
        //camHeight = CamWiggleObject.transform.localPosition.y;
        camHeadHeightDiff = p.topOfHead.transform.position.y - CamWiggleObject.transform.position.y;
        camTargetPoint = CamWiggleObject.transform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        if (p.IsDead) return; // Don't do anything if we're dead. Introduces a bug when dying in midair, but whatever

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
        cam.transform.localEulerAngles = new Vector3(pitch, 0, 0);

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
        CamWiggleObject.transform.localPosition = Vector3.SmoothDamp(CamWiggleObject.transform.localPosition, camTargetPoint, ref camLerpVelocity, camLerpSmoothTime);
        #endregion

        #region Crouching
        if (!p.IsCrouching && Input.GetKey(KeyCode.LeftControl))
        {
            p.IsCrouching = true;
            charCon.height = ccHeight * p.crouchHeightMultiplier;
            charCon.center = new Vector3(0f, charCon.height / 2, 0f);
            camTargetPoint = new Vector3(camTargetPoint.x, p.topOfHead.transform.localPosition.y - camHeadHeightDiff, camTargetPoint.z);
            // If in the air, raise our transform position by the same amount we move the camera down
            if (!charCon.isGrounded)
            {
                charCon.Move(new Vector3(0f, ccHeight - charCon.height, 0f));
                // Immediately set camera to new position
                CamWiggleObject.transform.localPosition = camTargetPoint;
            }
        }
        if (p.IsCrouching && !Input.GetKey(KeyCode.LeftControl) && charCon.isGrounded) // Do not allow uncrouching in the air
        {
            // check to see if we can uncrouch, by casting a ray up and seeing if it's clear
            bool canUncrouch = !Physics.Raycast(p.topOfHead.transform.position, Vector3.up, out RaycastHit crouchHit, ccHeight/2, layermask);
            if (canUncrouch)
            {
                p.IsCrouching = false;
                charCon.center = new Vector3(0f, ccHeight / 2, 0f);
                charCon.height = ccHeight;
                camTargetPoint = new Vector3(camTargetPoint.x, p.topOfHead.transform.localPosition.y - camHeadHeightDiff, camTargetPoint.z);
            }
        }
        #endregion

        #region Movement
        // Clean up tooltip text
        cursorTooltip.text = "";

        // Set grounded flag for the animation controller
        p.IsGrounded = charCon.isGrounded;

        // Get input values 
        p.frontBackMovement = Input.GetAxis("Vertical"); // NOTE These values are lerped!
        p.leftRightMovement = Input.GetAxis("Horizontal");

        // This makes footsteps sound better when stopping
        //p.isMoving = p.frontBackMovement != 0f || p.leftRightMovement != 0f; // We're moving if there is any input
        p.isMoving = Input.GetKey("w") || Input.GetKey("a") || Input.GetKey("s") || Input.GetKey("d");

        // Get forward/strafe direction
        Vector3 strafe = p.leftRightMovement * transform.right;
        Vector3 forward = p.frontBackMovement * transform.forward;
        Vector3 moveDirection = forward + strafe;
        moveDirection *= speed;

        if (charCon.isGrounded)
        {
            lastInput = moveDirection;
        } else
        {
            // If we're in the air, use the lastInput 
            moveDirection = lastInput;
            // Amend the lastInput to give _some_ air control
            moveDirection += 2f * (forward + strafe);
            // Ensure movement speed doesn't hit max
            moveDirection = Vector3.ClampMagnitude(moveDirection, speed);

            // TODO also we should code our own accelleration and not use horizontal/vertical input, that allows more 
            // control and to fix the max-speed-when-hitting-ground issue.
        }

        // Half speed if crouching or shift-walking
        if (p.IsCrouching || Input.GetKey(KeyCode.LeftShift))
        {
            p.isRunning = false;
            moveDirection *= 0.5f;
        } else
        {
            // If not crouch/shiftwalking but still moving, we're running
            p.isRunning = p.isMoving;
        }

        if (p.IsDead || p.isHealing) // Don't allow movement input if dead or healing, by overwriting input
        {
            moveDirection = new Vector3();
        }

        if (charCon.isGrounded)
        {
            fallingSpeed = -0.3f; // When on the ground we set a little downward speed otherwise the controller seems 
            // to 'bounce' on the ground and half the time doesn't consider itself grounded.
            // Go up if we're hitting jump.
            if (!p.IsDead && Input.GetKey("space"))
            {
                p.photonView.RPC("Jump", RpcTarget.All);
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
        if (Input.GetKeyDown("g") && p.heldItem)
        {
            TryDropHeldItem();
        }

        // Reload
        if (Input.GetKeyDown("r") && p.heldItem)
        {
            p.heldItem.SendMessage("Reload");
        }

        // Switch mesh renderer on/off
        if (Input.GetKeyDown("p"))
        {
            SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>(includeInactive:true);
            if (smr) smr.enabled = !smr.enabled;
        }

        if (Input.GetKeyDown("b"))
        {
            FindObjectOfType<UI_commandWindow>().photonView.RPC("HandleCommand",RpcTarget.MasterClient, "BOT");
        }

        // healing
        p.isHealing = Input.GetKey("h");
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
            p.aim = hit.point;

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

        } 
        else 
        {
            // We're not looking at anything in range, so clear the UI tooltip
            cursorTooltip.text = "";
        }

        // Rigidbody dragging
        if (Input.GetKeyDown(KeyCode.Z))
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
        if (p.heldItemScript)
        {
            p.heldItemScript.SetValues(rayOrigin, Input.GetButton("Fire1"));
        }
        #endregion
    }

    // TODO should be on player
    void TryPickupItem(GameObject item)
    {
        // If the player is already holding an item, drop it before picking up the new one
        if (p.heldItem)
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
            lm.LogError(logSrc, $"player {p.ID} tried to pick up missing item");
            return;
        }
        lm.Log(logSrc, $"player {p.ID} is picking up {pickup.nickname}");

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
        GameObject newItem = PhotonNetwork.Instantiate(pickup.prefabHeld.name, p.itemAnchor.transform.position, Quaternion.identity);

        // Server should destroy the original
        GameManager.gm.photonView.RPC("DestroyItem", RpcTarget.MasterClient, PV.ViewID);

        // TODO items with an audio source should be set to 2d spatial blend so the sound doesn't favour one speaker (annoying)

        // TODO define this layer somewhere. Does this even work properly?
        newItem.layer = 7;

    }

    // TODO yea all of this stuff should be on the player really

    // Drops our held item into the world. Called when we press G or on death
    public void TryDropHeldItem ()
    {
        if (!p.heldItem || !p.heldItemScript) return;
        TryDropItem(p.heldItemScript.worldPrefab.name);
        // Destroy the item in our hands.
        PhotonNetwork.Destroy(p.heldItem);
    }

    // Drops an item into the world
    public bool TryDropItem (string prefabName)
    {
        // TODO check for reasons we couldnt drop an item
        // Tell server we're dropping an item.
        photonView.RPC("RpcDropItem", RpcTarget.MasterClient, prefabName);
        // Do throw visuals on all clients
        photonView.RPC("DoThrowVisuals", RpcTarget.All);
        return true;
    }

    // Returns true if item was placed
    public bool TryPlaceItem (string prefabName, float distance)
    {
        if (CanPlaceItem(prefabName, distance))
        {
            // NOTE: we don't play a placement sound, this should be on the placed item
            PlaceItem(prefabName);
            return true;
        }
        // Play place-failure sound
        if (audioSource && cannotPlaceClip) audioSource.PlayOneShot(cannotPlaceClip);
        return false;
    }

    // Checks to see if we can place an item at our current looking point, considering a maximum distance.
    // Used when trying to place C4, trips, and cameras.
    public bool CanPlaceItem(string prefabName, float distance)
    {
        Vector3 rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
        bool hitSomething = Physics.Raycast(rayOrigin, cam.transform.forward, out RaycastHit hit, distance, layermask);
        // Only allow placing on static objects, which is hard to determine but for now anything with a lightmap index
        if (!hitSomething) return false;
        MeshRenderer mr = hit.collider.GetComponent<MeshRenderer>();
        return mr && mr.lightmapIndex > -1; // Items with no lightmap have index -1
    }

    // Places the provided prefab on whatever we're looking at. Things calling this should use CanPlaceItem beforehand
    public void PlaceItem (string prefabName)
    {
        PhotonNetwork.Instantiate(prefabName, lastHit.point, Quaternion.FromToRotation(Vector3.up, lastHit.normal));
    }

    [PunRPC]
    void RpcDropItem (string prefabName)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        lm.Log(logSrc,$"Player {p.ID} dropping item {prefabName}");
        // Create item being dropped
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefabName, cam.transform.position, transform.rotation);
        if (!go) return; // Account for errors when instantiating objects
        // Add some force so it moves away from the player who dropped it
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb) rb.AddForce(cam.transform.forward * 1000);
    }

    /// <summary>
    /// Attempts to activate an object by sending an activation request to the server.
    /// </summary>
    /// <param name="go">Object to be activated</param>
    /// <param name="position">Position at which the activation occurred, usually a raycast hit</param>
    void TryToActivate(GameObject go, Vector3 position)
    {
        if (p.IsDead) return;
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

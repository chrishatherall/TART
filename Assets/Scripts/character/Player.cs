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

public class Player : MonoBehaviourPun
{
    readonly string logSrc = "FPS_CTRL";

    [SerializeField]
    public int ID;

    // Ref to camera component, used for UI rendering
    public Camera cam;

    // last input, used when in the air
    Vector3 lastInput = Vector3.zero;

    // Create a layermask which ignores layer 7, so we dont constantly activate/shoot ourselves
    int layermask = ~(1 << 7);

    // Reference to the character we are currently controlling.
    public Character character;
    Character c { get => character; } // Just an easy internal ref to character
    // Reference to our Deathmatch script
    public DeathmatchPlayer DMPlayer;

    AudioSource audioSource; // Audio source for just this player, for UI sounds and such
    AudioListener audioListener;
    [SerializeField]
    AudioClip cannotPlaceClip;

    // The text box shown below our cursor, for displaying information on pickups, activatables, etc
    public UnityEngine.UI.Text cursorTooltip;

    float pitch = 0f; // desired character camera pitch, shouldnt be here

    // Use this for initialization
    void Awake()
    {
        // Set our id using the unique photonview
        ID = photonView.ControllerActorNr;
        // Find UI camera
        cam = GetComponentInChildren<Camera>();
        // Find audio source
        audioSource = GetComponent<AudioSource>();
        // Find audio listener
        audioListener = GetComponentInChildren<AudioListener>();

        // Announce self to GM.
        lm.Log(logSrc, "Started. Announcing to GameManager.");
        gm.AddPlayer(this);

        // Turn off the cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void TakeCharacterControl(Character c)
    {
        // Turn off our own audio listener
        audioListener.enabled = false;

        // First check the character Photon view is our OR can be owned by us
        // TODO if (photonView.IsMine)

        character = c;
        c.controllingPlayer = this;
        // Turn off the head so it doesn't clip with the camera
        c.GetComponent<PlayerAnimController>().SetHead(false);
        // Turn on character controller
        c.charCon.enabled = true;
        // Turn on camera
        c.Camera.enabled = true;
        // Turn on audio listener
        c.audioListener.enabled = true;
    }

    public void ReleaseCharacterControl()
    {
        if (!character)
        {
            lm.LogError(logSrc, "Cannot release character control, no character.");
            return;
        }

        

        // Turn on the head
        c.GetComponent<PlayerAnimController>().SetHead(true);
        // Turn off character controller
        c.charCon.enabled = false;
        // Turn off camera
        c.Camera.enabled = false;
        // Turn off audio listener
        c.audioListener.enabled = false;

        // Turn on our audio listener
        audioListener.enabled = true;

        c.controllingPlayer = null;
        character = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (!character) return;

        if (character.IsDead) return; // Don't do anything if we're dead. Introduces a bug when dying in midair, but whatever

        #region Camera
        // Control camera movement
        float yaw = c.transform.localEulerAngles.y + Input.GetAxis("Mouse X") * c.mouseSensitivity;
        // Support dumb inverted camera
        //float pitch = c.Camera.transform.localEulerAngles.x;
        //if (!invertCamera)
        //{
        pitch -= c.mouseSensitivity * Input.GetAxis("Mouse Y");
        //}
        //else
        //{
            // Inverted Y
        //    pitch += mouseSensitivity * Input.GetAxis("Mouse Y");
        //}
        // Clamp pitch between lookAngle
        pitch = Mathf.Clamp(pitch, -c.maxLookAngle, c.maxLookAngle);
        // Set y rotation of character object
        c.transform.localEulerAngles = new Vector3(0, yaw, 0);
        // Set x rotation of camera object
        c.Camera.transform.localEulerAngles = new Vector3(pitch, 0, 0);

        // Reduce camera wiggle
        // BIG NOTE: This is about camera wiggle, NOT gun recoil. Gun recoil uses this as a base, but has its own firing inaccuracy.
        // The reduction amount should be fraction of the x rotation, or CamWiggleReductionPerSecond, whichever is higher.
        float extraReduction = c.CamWiggleObject.transform.localEulerAngles.x;
        if (extraReduction > 180) extraReduction = 360 - extraReduction; // Normalise reduction that goes under 0.
        float reduction = c.CamWiggleReductionPerSecond + extraReduction * extraReduction; // Increase with a square, in case we get hit with something massive?
        // Rotate back toward 0
        Quaternion rotAmount = Quaternion.RotateTowards(c.CamWiggleObject.transform.localRotation, Quaternion.identity, reduction * Time.deltaTime);
        // Reduce wiggle value
        c._camWiggle = Mathf.Clamp(c._camWiggle - reduction * Time.deltaTime, 0f, 100f);
        c.CamWiggleObject.transform.localRotation = rotAmount;
        c.CamWiggleObject.transform.localPosition = Vector3.SmoothDamp(c.CamWiggleObject.transform.localPosition, c.camTargetPoint, ref c.camLerpVelocity, c.camLerpSmoothTime);
        #endregion

        #region Crouching
        if (!character.IsCrouching && Input.GetKey(KeyCode.LeftControl))
        {
            character.IsCrouching = true;
            c.charCon.height = c.ccHeight * character.crouchHeightMultiplier;
            c.charCon.center = new Vector3(0f, c.charCon.height / 2, 0f);
            c.camTargetPoint = new Vector3(c.camTargetPoint.x, character.topOfHead.transform.localPosition.y - c.camHeadHeightDiff, c.camTargetPoint.z);
            // If in the air, raise our transform position by the same amount we move the camera down
            if (!c.charCon.isGrounded)
            {
                c.charCon.Move(new Vector3(0f, c.ccHeight - c.charCon.height, 0f));
                // Immediately set camera to new position
                c.CamWiggleObject.transform.localPosition = c.camTargetPoint;
            }
        }
        if (character.IsCrouching && !Input.GetKey(KeyCode.LeftControl) && c.charCon.isGrounded) // Do not allow uncrouching in the air
        {
            // check to see if we can uncrouch, by casting a ray up and seeing if it's clear
            bool canUncrouch = !Physics.Raycast(character.topOfHead.transform.position, Vector3.up, out RaycastHit crouchHit, c.ccHeight/2, layermask);
            if (canUncrouch)
            {
                character.IsCrouching = false;
                c.charCon.center = new Vector3(0f, c.ccHeight / 2, 0f);
                c.charCon.height = c.ccHeight;
                c.camTargetPoint = new Vector3(c.camTargetPoint.x, character.topOfHead.transform.localPosition.y - c.camHeadHeightDiff, c.camTargetPoint.z);
            }
        }
        #endregion

        #region Movement
        // Clean up tooltip text
        cursorTooltip.text = "";

        // Set grounded flag for the animation controller
        character.IsGrounded = c.charCon.isGrounded;

        // Get input values 
        character.frontBackMovement = Input.GetAxis("Vertical"); // NOTE These values are lerped!
        character.leftRightMovement = Input.GetAxis("Horizontal");

        // This makes footsteps sound better when stopping
        //p.isMoving = p.frontBackMovement != 0f || p.leftRightMovement != 0f; // We're moving if there is any input
        character.isMoving = Input.GetKey("w") || Input.GetKey("a") || Input.GetKey("s") || Input.GetKey("d");

        // Get forward/strafe direction
        Vector3 strafe = character.leftRightMovement * c.transform.right;
        Vector3 forward = character.frontBackMovement * c.transform.forward;
        Vector3 moveDirection = forward + strafe;
        // Stop us going faster diagonally. If we're moving on multiple axis, scale back down
        if (moveDirection.magnitude > 1f) moveDirection = moveDirection.normalized;
        // Adjust direction by speed
        moveDirection *= c.speed;

        if (c.charCon.isGrounded)
        {
            lastInput = moveDirection;
        } else
        {
            // If we're in the air, use the lastInput 
            moveDirection = lastInput;
            // Amend the lastInput to give _some_ air control
            moveDirection += 2f * (forward + strafe);
            // Ensure movement speed doesn't hit max
            moveDirection = Vector3.ClampMagnitude(moveDirection, c.speed);

            // TODO also we should code our own accelleration and not use horizontal/vertical input, that allows more 
            // control and to fix the max-speed-when-hitting-ground issue.
        }

        // Half speed if crouching or shift-walking
        if (character.IsCrouching || Input.GetKey(KeyCode.LeftShift))
        {
            character.isRunning = false;
            // Cheap fix for air-crouchnig slowing you down, only actually reduce speed if grounded
            if (character.IsGrounded) moveDirection *= 0.5f;
        } else
        {
            // If not crouch/shiftwalking but still moving, we're running
            character.isRunning = character.isMoving;
        }

        if (character.IsDead || character.isHealing) // Don't allow movement input if dead or healing, by overwriting input
        {
            moveDirection = new Vector3();
        }

        if (c.charCon.isGrounded)
        {
            c.fallingSpeed = -0.3f; // When on the ground we set a little downward speed otherwise the controller seems 
            // to 'bounce' on the ground and half the time doesn't consider itself grounded.
            // Go up if we're hitting jump.
            if (!character.IsDead && Input.GetKey("space"))
            {
                character.photonView.RPC("Jump", RpcTarget.All);
                c.fallingSpeed = c.jumpStrength;
            }
        }
        else
        {
            // Increase gravity if we're in the air
            c.fallingSpeed += c.gravity * Time.deltaTime;
        }

        // Apply falling speed to move direction
        moveDirection.y = c.fallingSpeed;

        // Instruct the controller to move us
        c.charCon.Move(moveDirection * Time.deltaTime);

        #endregion

        #region Key-specific events
        // Throw objects
        if (Input.GetKeyDown("g") && character.heldItem)
        {
            TryDropHeldItem();
        }

        // Reload
        if (Input.GetKeyDown("r") && character.heldItem)
        {
            character.heldItem.SendMessage("Reload");
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
        character.isHealing = Input.GetKey("h");
        #endregion

        #region Interaction



        // Raycast forward from our camera to see if we're looking at anything important within range.
        //if (!cam) cam = GetComponentInChildren<Camera>();
        Vector3 rayOrigin = c.Camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));
        bool hitSomething = Physics.Raycast(rayOrigin, c.Camera.transform.forward, out RaycastHit hit, 500f, layermask);
        if (hitSomething)
        {
            // Set our last hit to this point
            c.lastHit = hit;
            character.aim = hit.point;

            // We can't pickup or activate anything beyond our range.
            if (Vector3.Distance(rayOrigin, hit.point) < c.activateRange)
            {
                // Find a pickup
                Pickup pickup = c.lastHit.transform.GetComponent<Pickup>();
                if (pickup && pickup.enabled == true)
                {
                    // Set our ui tooltip
                    cursorTooltip.text = "[E] Pick up " + pickup.nickname;

                    // Try to pick up items
                    if (Input.GetKeyDown(KeyCode.E)) TryPickupItem(pickup.gameObject);
                }

                // Find an activatable
                Activatable act = c.lastHit.transform.GetComponent<Activatable>();
                if (act)
                {
                    // Set our ui tooltip
                    cursorTooltip.text = "[E] Activate " + act.nickname;

                    // Try to activate objects
                    if (Input.GetKeyDown(KeyCode.E)) TryToActivate(c.lastHit.transform.gameObject, c.lastHit.point);
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
            if (c.fj.connectedBody)
            {
                // Try to transfer ownership back to scene
                PhotonView pv = c.fj.connectedBody.GetComponent<PhotonView>();
                if (pv && pv.OwnershipTransfer == OwnershipOption.Takeover)
                {
                    pv.TransferOwnership(0);
                }

                c.fj.connectedBody.AddForce(new Vector3(0f, 0.0001f));
                c.fj.connectedBody = null;

            } else if (hitSomething)
            {
                // Try to drag
                Rigidbody rb = c.lastHit.transform.GetComponent<Rigidbody>();
                PhotonView pv = c.lastHit.transform.GetComponent<PhotonView>();
                if (rb && pv && pv.OwnershipTransfer == OwnershipOption.Takeover) { 
                    // Take ownership of this object so we can send physics updates
                    pv.TransferOwnership(this.photonView.Owner);
                    // Set our dragger at the hit position
                    c.rbDragger.transform.position = c.lastHit.transform.position;
                    c.fj.connectedBody = rb;
                }
            }
        }

        #endregion

        #region Misc
        // Tell held item about some stuff
        if (character.heldItemScript)
        {
            character.heldItemScript.SetValues(rayOrigin, Input.GetButton("Fire1"));
        }
        #endregion
    }

    public void PlaySound(AudioClip clip)
    {
        if (audioSource) audioSource.PlayOneShot(clip);
    }

    void TryPickupItem(GameObject item)
    {
        // If the player is already holding an item, drop it before picking up the new one
        if (character.heldItem)
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
            lm.LogError(logSrc, $"player {character.ID} tried to pick up missing item");
            return;
        }
        lm.Log(logSrc, $"player {character.ID} is picking up {pickup.nickname}");

        // Play sound if the pickup has one
        if (pickup.pickupSound)
        {
            c.audioSource.clip = pickup.pickupSound;
            c.audioSource.Play();
        }

        // Check to see if we're picking up a non-held item like a key
        if (!pickup.prefabHeld)
        {
            gm.Alert("Picked up " + pickup.nickname);
            c.inventory.AddItem(pickup);
            Destroy(item); // Note, this only destroys the item for us
            return;
        }

        // Create new item in our hands
        object[] instanceData = new object[1];
        instanceData[0] = character.ID;
        GameObject newItem = PhotonNetwork.Instantiate(pickup.prefabHeld.name, character.itemAnchor.transform.position, Quaternion.identity, 0, instanceData);

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
        if (!character.heldItem || !character.heldItemScript) return;
        TryDropItem(character.heldItemScript.worldPrefab.name);
        // Destroy the item in our hands.
        PhotonNetwork.Destroy(character.heldItem);
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
        if (c.audioSource && cannotPlaceClip) c.audioSource.PlayOneShot(cannotPlaceClip);
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
        PhotonNetwork.Instantiate(prefabName, c.lastHit.point, Quaternion.FromToRotation(Vector3.up, c.lastHit.normal));
    }

    [PunRPC]
    void RpcDropItem (string prefabName)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        lm.Log(logSrc,$"Player {character.ID} dropping item {prefabName}");
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
        if (character.IsDead) return;
        // Find an activatable
        Activatable act = go.GetComponent<Activatable>();
        if (!act) return;
        // Check we have the right key if it's required
        if (act.requiredKey != "" && !c.inventory.HasItem(act.requiredKey))
        {
            // ui alert
            gm.Alert("Need " + act.requiredKey);
            lm.Log(logSrc, "Need " + act.requiredKey);
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using TART;
using static GameManager;
using static LogManager;

// Delegate signature for empty events // TODO no idea if I actually need this
public delegate void EmptyEvent();

public class Character : MonoBehaviourPunCallbacks, IPunObservable, IPunInstantiateMagicCallback
{
    readonly string logSrc = "Character";

    // Unique character ID
    [SerializeField]
    public int ID; // Cheap way to serialise

    // Role
    private TartRole _role;

    #region camera
    public Camera Camera { get => cam; }
    [SerializeField]
    Camera cam;
    // Public variables
    public float mouseSensitivity = 1f;
    public float maxLookAngle = 80f;
    // Camera wiggle. Every time this is set, the rotation of the CameraWiggler object is randomised (to this scale).
    // Eg, a C4 knock will set this very high once. Guns will set this low quite often.
    // This is reduced over time by a set amount.
    public float _camWiggle; // Effectively degrees of rotation for x/y.
    public float CamWiggleReductionPerSecond = 6f;
    // Ref to the wiggle object, parent of our camera
    public GameObject CamWiggleObject;
    // The difference between our head height and our cam height
    public float camHeadHeightDiff;
    // The target camera position for our cam lerper. Adjusted when crouching
    public Vector3 camTargetPoint;
    public float camLerpSmoothTime = 0.1f;
    public Vector3 camLerpVelocity = Vector3.zero;
    // The last raycast hit of our camera
    public RaycastHit lastHit;

    public float CameraWiggle
    {
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
    #endregion

    // Ref to our dragger script
    Dragger dragger;
    public bool IsDraggingObject { get => dragger && dragger.draggingObject; }

    #region Character movement
    // Character move speed.
    public float speed = 4.0f;
    // Strength of gravity
    [SerializeField]
    public float gravity = -10f;
    // Speed at which the character is falling.
    [SerializeField]
    public float fallingSpeed = 0f;
    // Strength of a jump.
    [SerializeField]
    public float jumpStrength = 4f;
    public GameObject topOfHead;
    public Vector3 standingHeadPos;
    // Movement values for animation controller
    public bool _isGrounded;
    public float frontBackMovement;
    public float leftRightMovement;
    public bool _isCrouching;
    public bool isMoving;
    public bool isRunning;
    public float crouchHeightMultiplier = 0.66f;
    // Velocity 
    public float[] last3FramesVelocity = { 1f, 1f, 1f };
    public CharacterController charCon;
    public float ccHeight;
    #endregion

    #region Sync'd variables
    // Oil is effectively hitpoints
    public int oil = 100;
    public int maxOil = 100;
    // is the player dead
    private bool _isDead = false;
    // Name of this character
    public string nickname;
    // Character is loaded and ready
    public bool isReady = false;
    // The thing this character is looking at
    public Vector3 aim;
    #endregion

    // Range at which we can activate buttons/doors
    [SerializeField]
    public float activateRange = 2f;

    // Ref to the character inventory
    public Inventory inventory;

    // The audio source we use to emit sounds from this character
    public AudioSource audioSource;
    // Audio listener on this character
    public AudioListener audioListener;

    // Flag for bots
    public bool isBot;

    // Layer mask which ignores layer 7 (local player)
    int layermask = ~(1 << 7);

    // Ref to potential player controlling us. Null for bots
    public Player controllingPlayer;

    // Footsteps
    [SerializeField]
    float footstepInterval = 0.5f; // Time between footsteps
    float sLastFootstep; // Seconds since last footstep TODO not technically true, change this along with sfootsteplastplayed
    [SerializeField]
    float fFootstepVolume = 0.3f;
    [SerializeField]
    float sFootstepCooldown = 0.2f; // Time in seconds between max footstep sounds
    float sFootstepLastPlayed;
    float maxFootstepVolume = 10f;
    float minFootstepVolume = 0.2f;

    // Jump event
    public event EmptyEvent OnJump;

    // Our body parts
    public BodyPart[] bodyParts;

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;

    [SerializeField]
    AudioClip healSound;

    [SerializeField]
    AudioClip damageSound;

    [SerializeField]
    AudioClip deathSound;

    [SerializeField]
    AudioClip throwSound;

    // Public access for the role
    public TartRole Role { get => _role; }

    public bool IsDead { 
        get => _isDead;
        set
        {
            if (_isDead != value)
            {
                _isDead = value;
                if (_isDead) Die();
            }
        }
    }

    public bool IsCrouching { 
        get => _isCrouching;
        set
        {
            // Crouch or uncrouch if the value has changed
            if (_isCrouching != value)
            {
                if (value)
                {
                    _isCrouching = true;
                    // Lower the top of our head to crouching height (which is half)
                    topOfHead.transform.localPosition = standingHeadPos * crouchHeightMultiplier;
                } else {
                    _isCrouching = false;
                    // Raise the top of our head to standing height
                    topOfHead.transform.localPosition = standingHeadPos;
                }
            }
            
        }
    }

    public bool IsGrounded { 
        get => _isGrounded;
        set
        {
            // If the value changed and we're now grounded, do loud footstep
            if (_isGrounded != value && value)
            {
                DoFootstep(last3FramesVelocity[2]);
            }
            _isGrounded = value;
        }
    }

    // Healing
    public bool isHealing;
    public float sSinceLastHeal = 0f;
    public float sHealInterval = 1f;

    // Cause of death
    bool diedToInstantDeath = false;
    int instantDeathCharacter = -1;
    string instantDeathWeapon = "Unknown";

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // See if this character should be a bot
        isBot = (string)info.photonView.InstantiationData[0] == "isBot";

        // Set our Id using the unique photonView id
        ID = photonView.ViewID;

        // GM might not be ready yet.
        if (!gm)
        {
            lm.LogError(logSrc,"GM not ready!");
            return;
        }

        charCon = GetComponent<CharacterController>();

        // Set some character controller/camera heights
        ccHeight = charCon.height;
        //camHeight = CamWiggleObject.transform.localPosition.y;
        camHeadHeightDiff = topOfHead.transform.position.y - CamWiggleObject.transform.position.y;
        camTargetPoint = CamWiggleObject.transform.localPosition;

        // Set ourselves as default role
        this._role = gm.GetRoleFromID(0);

        // Find our body parts
        bodyParts = GetComponentsInChildren<BodyPart>();

        // Find our audio source
        audioSource = GetComponent<AudioSource>();

        // Set our standing head height to whatever it is when we spawned
        standingHeadPos = topOfHead.transform.localPosition;

        // Set dragger stuff
        dragger = GetComponentInChildren<Dragger>();

        // Set as ready, and apply nickname when the local player has loaded
        if (photonView.IsMine) {
            if (isBot)
            {
                nickname = "BOT_" + ID;
            }
            else
            {
                nickname = PhotonNetwork.LocalPlayer.NickName;
                SetLayer(7); // Set to localplayer layer
            }
            isReady = true;
        }

        // Announce self to GM.
        lm.Log(logSrc, "Started. Announcing to GameManager.");
        gm.AddCharacter(this);

        // Parent self to gm's spawned-players object for cleanliness
        this.transform.parent = gm.characterSpawnParent;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // TODO sync bodypart damage so we can create damage visuals on specific body parts

        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(oil);
            stream.SendNext(maxOil);
            stream.SendNext(IsDead);
            stream.SendNext(IsCrouching);
            stream.SendNext(isReady);
            stream.SendNext(nickname);
            stream.SendNext(ID);
            stream.SendNext(aim);

            stream.SendNext(frontBackMovement);
            stream.SendNext(leftRightMovement);
            stream.SendNext(isMoving);
            stream.SendNext(isRunning);
            stream.SendNext(IsGrounded);
            stream.SendNext(SerialiseBodyPartDamage());
        }
        else
        {
            // Network player, receive data
            this.oil = (int)stream.ReceiveNext();
            this.maxOil = (int)stream.ReceiveNext();
            this.IsDead = (bool)stream.ReceiveNext();
            this.IsCrouching = (bool)stream.ReceiveNext();
            this.isReady = (bool)stream.ReceiveNext();
            this.nickname = (string)stream.ReceiveNext();
            this.ID = (int)stream.ReceiveNext();
            this.aim = (Vector3)stream.ReceiveNext();

            this.frontBackMovement = (float)stream.ReceiveNext();
            this.leftRightMovement = (float)stream.ReceiveNext();
            this.isMoving = (bool)stream.ReceiveNext();
            this.isRunning = (bool)stream.ReceiveNext();
            this.IsGrounded = (bool)stream.ReceiveNext();
            DeserialiseBodyPartDamage((string)stream.ReceiveNext());
        }
    }

    [PunRPC]
    public void SetRoleById(int id)
    {
        _role = gm.GetRoleFromID(id);
        // Create an alert for the local player
        if (photonView.IsMine && !isBot) gm.Alert($"Role is now {_role.Name}!");
    }

    [PunRPC]
    public void Reset(bool forceRespawn)
    {
        // Set new position if dead or being forced to respwn
        if (IsDead || forceRespawn)
        {
            // Turn off the character controller before force-moving, or it'll just set us right back.
            CharacterController charCon = GetComponent<CharacterController>();
            bool charConWasEnabled = charCon && charCon.enabled;
            if (charConWasEnabled) charCon.enabled = false;
            Transform newTransform = gm.GetCharacterSpawnLocation();
            this.transform.position = newTransform.position;
            this.transform.rotation = newTransform.rotation;
            if (charConWasEnabled) charCon.enabled = true;
        }
        oil = 100;
        maxOil = 100;
        // Set damage of each BodyPart to 0
        foreach(BodyPart bp in bodyParts) { bp.Reset(); }
        IsDead = false;
        this._role = gm.GetRoleFromID(0);

        SetRagdoll(false);

        diedToInstantDeath = false;
        instantDeathCharacter = -1;

        // Kind of a dirty fix, but works until we have a UI manager.
        if (photonView.IsMine) Cursor.lockState = CursorLockMode.Locked;
    }

    // Called from a remote/local BodyPart script when it is damaged.
    [PunRPC]
    public void DamageBone(string bodyPartName, int dmg, Vector3 hitDirection, int sourceCharacterID, string sourceWeapon)
    {
        // Every client receives this so we can do visuals

        // Can't take more damage if we're dead
        if (IsDead) return;

        // Play damage sound if our local player is controlling this character
        if (gm.localPlayer.character == this) audioSource.PlayOneShot(damageSound);

        BodyPart bp = GetBodyPartByName(bodyPartName);
        if (bp)
        {
            // Apply damage to the BodyPart
            bp.AddDamage(dmg, sourceCharacterID, sourceWeapon);
            // Apply force to the BodyPart
            bp.AddForce(hitDirection);
        }

        // To make damage more responsive, kill player instantly if damage > oil
        if (GetDamage() > oil)
        {
            Kill(hitDirection, sourceCharacterID, sourceWeapon);
        }
    }

    // This is to allow the server to kill a character easily
    [PunRPC]
    void InstaKill(int dmg)
    {
        DamageBone("B-head", dmg, Vector3.up, -1, "Command"); // Source of the damage is character -1, essentially nobody
    }

    public void KillSelf()
    {
        photonView.RPC("InstaKill", RpcTarget.All, 999);
    }

    // TODO only heals 1 at a time. Ideally should distribute the healing to all BPs that need it, ratiod
    public void Heal(int amount)
    {
        // Run loop once for each heal amount
        int healedAmount = 0;
        for (int i = 0; i < amount; i++)
        {
            // Get a bodypart with damage
            BodyPart bp = bodyParts.FirstOrDefault(bp => bp.CurrentDamage > 0);
            if (!bp) break;
            // Reduce damage by 1
            bp.RemoveDamage(1);
            healedAmount++;
        }

        // Do visuals if we healed anything
        if (healedAmount > 0) photonView.RPC("DoHealVisuals", RpcTarget.All);
    }

    void DoFootstep(float volumeMultiplier)
    {
        // Ensure volume is positive
        if (volumeMultiplier < 0) volumeMultiplier *= -1;
        // Ignore small values
        if (volumeMultiplier < minFootstepVolume) return;
        // Limit volume
        if (volumeMultiplier > maxFootstepVolume) volumeMultiplier = maxFootstepVolume;
        // Make sure our last footstep is over our cooldown
        if (Time.time - sFootstepLastPlayed < sFootstepCooldown) return;
        sLastFootstep = 0f;
        // Cast a ray down at the thing under our feet
        bool hit = Physics.Raycast(this.transform.position, Vector3.down, out RaycastHit footstepHit, 1f);
        if (!hit) return;
        if (audioSource) audioSource.PlayOneShot(gm.GetFootstepByMaterial(footstepHit.collider.material), fFootstepVolume * volumeMultiplier);
        sFootstepLastPlayed = Time.time;
    }

    // Called on all clients
    [PunRPC]
    public void Jump()
    {
        // Invoke the jump event
        OnJump.Invoke();
    }

    [PunRPC]
    public void DoHealVisuals()
    {
        // Play sound
        if (audioSource && healSound) audioSource.PlayOneShot(healSound);
    }

    [PunRPC]
    public void DoThrowVisuals()
    {
        // Play sound
        if (audioSource && throwSound) audioSource.PlayOneShot(throwSound);
    }

    // Ticks happen once a second. Why though? Is it so we reduce network sync traffic?
    // It would be cleaner to ignore this and round values for the UI.
    private float msSinceLastTick = 0;
    public void Update()
    {
        // Footsteps
        if (!IsDead && IsGrounded && isRunning)
        {
            sLastFootstep += Time.deltaTime;
            if (sLastFootstep > footstepInterval)
            {
                DoFootstep(1);
            }
        }
        if (charCon)
        {
            last3FramesVelocity[2] = last3FramesVelocity[1];
            last3FramesVelocity[1] = last3FramesVelocity[0];
            last3FramesVelocity[0] = charCon.velocity.y;
        }

        if (!photonView.IsMine) return;

        // Track healing time
        if (isHealing && !IsDead)
        {
            sSinceLastHeal += Time.deltaTime;
            if (sSinceLastHeal > sHealInterval)
            {
                Heal(5);
                sSinceLastHeal = 0f;
            }
        } else {
            sSinceLastHeal = 0f;
        }

        // Track DOT
        msSinceLastTick += Time.deltaTime;
        if (msSinceLastTick > 1) // One second
        {
            msSinceLastTick = 0;

            int dmg = GetDamage();

            // Leak oil if damaged
            if (dmg > 0 && oil > 0)
            {
                if (!IsDead) audioSource.PlayOneShot(damageSound);
                oil -= dmg; // TODO: Hook into this change to create oil leaks on the floor. Oil can still leak if dead via other means, creating cool oil pools.
                // If we're out of oil and not dead, die.
                if (oil <= 0 && !IsDead)
                {
                    oil = 0;
                    IsDead = true;
                }
            }
        }
    }

    BodyPart GetBodyPartByName(string name)
    {
        return bodyParts.FirstOrDefault(bp => bp.gameObject.name == name);
    }

    // Serialises the damage of our BodyParts into a string
    string SerialiseBodyPartDamage ()
    {
        string[] bpData = new string[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
        {
            bpData[i] = bodyParts[i].Serialise(); //bodyParts[i].gameObject.name + ":" + bodyParts[i].Damage;
        }
        return string.Join("/", bpData);
    }

    void DeserialiseBodyPartDamage (string data)
    {
        // The first frame we might not have any bones, so ignore 0-length data. BodyParts can also be null on startup
        if (data.Length == 0 || bodyParts == null || bodyParts.Length == 0) return;

        string[] parts = data.Split('/');
        foreach (string part in parts)
        {
            string[] split = part.Split(';');
            // Find a BodyPart by name of split[0]
            // bp-head;1001:4:1002:8;transformdata
            BodyPart bp = GetBodyPartByName(split[0]);
            if (!bp)
            {
                lm.LogError(logSrc, $"Error deserialising BodyPart damage, invalid part name. Name: '{split[0]}'");
                return;
            }
            bp.Deserialise(part);
        }
    }

    public int GetDamage()
    {
        return bodyParts.Sum(bp => bp.CurrentDamage);
    }

    // Turns ragdoll on/off
    void SetRagdoll(bool ragdoll)
    {
        // Change the rigidbody of all of our body parts to ragdoll mode
        foreach (BodyPart bp in bodyParts)
        {
            Rigidbody rb = bp.GetComponent<Rigidbody>();
            rb.isKinematic = !ragdoll;
            rb.useGravity = ragdoll;
        }

        // If we're a local player we need to turn our controller off. TODO I don't like this being here but it needs to be off before we apply ragdoll
        if (charCon) charCon.enabled = !ragdoll;

        // Find an Animator and turn it off
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = !ragdoll;
    }

    // Instantly kills this player. This is called on all clients when something instantly kills someone, to replicate the ragdoll nicely.
    // TODO This seems messy
    public void Kill(Vector3 hitDirection, int characterId, string sourceWeapon)
    {
        diedToInstantDeath = true;
        instantDeathCharacter = characterId;
        instantDeathWeapon = sourceWeapon;
        //if (!photonView.IsMine)
        //{
        //    lm.LogError(logSrc, "Tried to Kill a non-local player");
        //    return;
        //}
        IsDead = true;
        // Ragdoll should be enabled now that we're dead, so apply hit force
        foreach (BodyPart bp in bodyParts)
        {
            Rigidbody rb = bp.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.AddForce(bp.cumulativeForce, ForceMode.Impulse);
            }
        }
    }

    // Called when when isdead is set to true
    void Die()
    {


        SetRagdoll(true);

        // Determine cause of death
        // As you can't currently restore lost oil, the person who killed us is the person who dealt the most BP damage, unless instant killed
        string murdererName = "Unknown";
        int murdererID = 0;
        string causeOfDeath = "Unknown";
        if (diedToInstantDeath)
        {
            Character murderer = gm.GetCharacterById(instantDeathCharacter);
            murdererName = murderer ? murderer.nickname : "Unknown";
            murdererID = instantDeathCharacter;
            causeOfDeath = instantDeathWeapon;
        }
        else
        {
            // Calculate killer by bp damage
            // Merge all bodypart damages
            List<Damage> sumDamages = new List<Damage>();
            foreach (BodyPart bp in bodyParts)
            {
                foreach (Damage D in bp.Damages)
                {
                    // Find an entry in sumDamages for this player id
                    Damage existing = sumDamages.FirstOrDefault(ED => ED.SourceCharacterId == D.SourceCharacterId);
                    if (existing != null)
                    {
                        existing.Amount += D.Amount;
                    }
                    else
                    {
                        sumDamages.Add(D);
                    }
                }
            }
            // Find the biggest damage dealer
            if (sumDamages.Count == 0)
            {
                lm.LogError(logSrc, $"Player {nickname} died without damaged BodyParts or instant death");
            } else
            {
                Damage murderer = sumDamages[0];
                foreach (Damage D in sumDamages)
                {
                    if (D.Amount > murderer.Amount) murderer = D;
                }
                murdererID = murderer.SourceCharacterId;
                Character cMurderer = gm.GetCharacterById(murderer.SourceCharacterId);
                if (cMurderer)
                {
                    murdererName = cMurderer.nickname;
                } else
                {
                    lm.LogError(logSrc, $"Could not find murderer character with id of {murdererID}");
                }
                causeOfDeath = "Oil Loss";
            }
        }
        // Raise a death event
        object[] args = { ID, murdererID, causeOfDeath };
        gm.RaiseEvent(Events.CharacterDied, args);

        lm.Log(logSrc, $"[{ID}]{nickname} was killed by [{murdererID}]{murdererName} via {causeOfDeath}.");

        // We don't need to do anything else if this isn't our character
        if (!this.photonView.IsMine) return;

        // Play death sound
        audioSource.PlayOneShot(deathSound);

        // TODO this might be better hooking into an event to increase modularity
        // Try to drop our held item
        TryDropHeldItem();

        // Dont do any UI stuff for bots
        if (!isBot)
        {
            // Turn on the dead screen
            gm.DeadScreen.SetActive(true);

            // Set dead screen message
            gm.DeathDetailsText.text = $"Killed by {murdererName} via {causeOfDeath}";

            // Activate mouse
            Cursor.lockState = CursorLockMode.None;
        }
    }

    void SetLayer(int layer)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: true))
        {
            t.gameObject.layer = layer;
        }
    }

    public void StartDraggingItem(Draggable d)
    {
        if (!dragger || !d || !d.enabled) return;
        // Take ownership of this object so we can send physics updates
        d.pv.TransferOwnership(this.photonView.Owner);
        // Tell everyone we're dragging this item
        photonView.RPC("DragChange", RpcTarget.All, false, d.pv.ViewID, d.name);
    }

    public void StopDraggingItem()
    {
        if (!dragger || !dragger.draggingObject) return;
        // Try to transfer ownership back to scene.
        // TODO if we do this instantly things tend to rubberband back a lot. Maybe delay this, or tweak transform sync
        // TODO maybe even look for way to auto-transfer objects when the player leaves?
        dragger.draggingObject.pv.TransferOwnership(0);
        // Tell everyone we're not dragging this item
        photonView.RPC("DragChange", RpcTarget.All, true, dragger.draggingObject.pv.ViewID, dragger.draggingObject.name);
    }

    [PunRPC]
    void DragChange (bool dropping, int photonViewID, string partName)
    {
        if (!dragger) return;
        if (dropping)
        {
            dragger.Drop();
        } else
        {
            // Find the PhotonView and Draggable
            PhotonView pv = PhotonView.Find(photonViewID);
            if (!pv)
            {
                lm.LogError(logSrc, $"Cannot handle DragChange, no PhotonView of ID {photonViewID}");
                return;
            }
            Draggable d = pv.GetComponent<Draggable>();
            // If doesnt exist, look for component in children that matches partname
            if (!d) d = pv.GetComponentsInChildren<Draggable>().First(drag => drag.name == partName);
            if (!d)
            {
                lm.LogError(logSrc, $"Cannot handle DragChange, no Draggable of name {partName}");
                return;
            }
            dragger.Drag(d);
        }
    }

    public void TryPickupItem(GameObject item)
    {
        // If the player is already holding an item, drop it before picking up the new one
        if (heldItem)
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
            lm.LogError(logSrc, $"character {ID} tried to pick up missing item");
            return;
        }
        lm.Log(logSrc, $"character {ID} is picking up {pickup.nickname}");

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
        object[] instanceData = new object[1];
        instanceData[0] = ID;
        GameObject newItem = PhotonNetwork.Instantiate(pickup.prefabHeld.name, itemAnchor.transform.position, Quaternion.identity, 0, instanceData);

        // Server should destroy the original
        GameManager.gm.photonView.RPC("DestroyItem", RpcTarget.MasterClient, PV.ViewID);

        // TODO items with an audio source should be set to 2d spatial blend so the sound doesn't favour one speaker (annoying)

        // TODO define this layer somewhere. Does this even work properly?
        newItem.layer = 7;

    }

    // Drops our held item into the world. Called when we press G or on death
    public void TryDropHeldItem()
    {
        if (!heldItem || !heldItemScript) return;
        TryDropItem(heldItemScript.worldPrefab.name);
        // Destroy the item in our hands.
        PhotonNetwork.Destroy(heldItem);
    }

    // Drops an item into the world
    public bool TryDropItem(string prefabName)
    {
        // TODO check for reasons we couldnt drop an item
        // Tell server we're dropping an item.
        photonView.RPC("RpcDropItem", RpcTarget.MasterClient, prefabName);
        // Do throw visuals on all clients
        photonView.RPC("DoThrowVisuals", RpcTarget.All);
        return true;
    }

    // Returns true if item was placed
    public bool TryPlaceItem(string prefabName, float distance)
    {
        if (CanPlaceItem(prefabName, distance))
        {
            // NOTE: we don't play a placement sound, this should be on the placed item
            PlaceItem(prefabName);
            return true;
        }
        // Play place-failure sound
        if (audioSource && gm.cannotPlaceClip) audioSource.PlayOneShot(gm.cannotPlaceClip);
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
    void PlaceItem(string prefabName)
    {
        PhotonNetwork.Instantiate(prefabName, lastHit.point, Quaternion.FromToRotation(Vector3.up, lastHit.normal));
    }

    [PunRPC]
    void RpcDropItem(string prefabName)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        lm.Log(logSrc, $"Character {ID} dropping item {prefabName}");
        // Create item being dropped
        object[] instanceData = new object[1];
        instanceData[0] = ID;
        GameObject go = PhotonNetwork.InstantiateRoomObject(prefabName, cam.transform.position, transform.rotation, 0, instanceData);
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
    public void TryToActivate(GameObject go, Vector3 position)
    {
        if (IsDead) return;
        // Find an activatable
        Activatable act = go.GetComponent<Activatable>();
        if (!act) return;
        // Check we have the right key if it's required
        if (act.requiredKey != "" && !inventory.HasItem(act.requiredKey))
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

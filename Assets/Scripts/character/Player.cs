using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using tart;
using static GameManager;
using static LogManager;

// Delegate signature for empty events // TODO no idea if I actually need this
public delegate void EmptyEvent();

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    readonly string logSrc = "Player";

    // Role
    private TartRole _role;

    #region Sync'd variables
    // Oil is effectively hitpoints
    public int oil = 100;
    public int maxOil = 100;
    // is the player dead
    private bool _isDead = false;
    // Local player is loaded and ready to play a round
    public bool isReady = false;
    // Name of this player
    public string nickname;
    // The unique actor number provided by photon to networked players (alias is ID)
    public int actorNumber;
    // The thing this player is looking at
    public Vector3 aim;
    #endregion

    // Flag for bots
    public bool isBot;

    // Movement
    public GameObject topOfHead;
    Vector3 standingHeadPos;
    // Movement values for animation controller
    bool _isGrounded;
    public float frontBackMovement;
    public float leftRightMovement;
    bool _isCrouching;
    public bool isMoving;
    public bool isRunning;
    public float crouchHeightMultiplier = 0.66f;

    // Footsteps
    [SerializeField]
    float footstepInterval = 0.5f; // Time between footsteps
    float sLastFootstep; // Seconds since last footstep
    [SerializeField]
    float fFootstepVolume = 0.3f;

    // Jump event
    public event EmptyEvent OnJump;

    // Our body parts
    BodyPart[] bodyParts;

    // Our held item, and script
    public GameObject heldItem;
    public HeldItem heldItemScript;
    // The item anchor gameobject
    public GameObject itemAnchor;
    // The parent of the held-item anchor we need to move up/down when crouching
    Transform itemAnchorParent;
    // The difference in position between our item anchor parent and topOfHead. Used to calculate crouched position
    Vector3 itemAnchorParentHeadDiff;


    AudioSource audioSrc;
    [SerializeField]
    AudioClip healSound;

    [SerializeField]
    AudioClip damageSound;

    [SerializeField]
    AudioClip deathSound;

    // Public access for the role
    public TartRole Role { get => _role; }

    // ID is a alias for actorNumber
    public int ID { get => actorNumber; }
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
                    // Keep our item anchor relative to the top of the head
                    itemAnchorParent.transform.position = topOfHead.transform.position + itemAnchorParentHeadDiff;
                } else {
                    _isCrouching = false;
                    // Raise the top of our head to standing height
                    topOfHead.transform.localPosition = standingHeadPos;
                    // Keep our item anchor relative to the top of the head
                    itemAnchorParent.transform.position = topOfHead.transform.position + itemAnchorParentHeadDiff;
                }
            }
            
        }
    }

    public bool IsGrounded { 
        get => _isGrounded;
        set
        {
            // If the value changed and we're now grounded, do loud footstep
            if (_isGrounded != value && value) DoFootstep(3);
            _isGrounded = value;
        }
    }

    // Healing
    public bool isHealing;
    float sSinceLastHeal = 0f;

    void Start()
    {
        // GM might not be ready yet.
        if (!gm)
        {
            lm.LogError(logSrc,"GM not ready!");
            return;
        }

        // Set ourselves as default role
        this._role = gm.GetRoleFromID(0);

        // Find our body parts
        bodyParts = GetComponentsInChildren<BodyPart>();

        // Find our audio source
        audioSrc = GetComponent<AudioSource>();

        // Set our standing head height to whatever it is when we spawned
        standingHeadPos = topOfHead.transform.localPosition;
        // Set our standing itemAnchorHeight
        itemAnchorParent = itemAnchor.transform.parent;
        itemAnchorParentHeadDiff = itemAnchorParent.transform.position- topOfHead.transform.position;

        // Set as ready, and apply nickname when the local player has loaded
        if (photonView.IsMine)
        {
            if (isBot)
            {
                actorNumber = 10000 + this.photonView.ViewID; // Probably not the safest
                nickname = "BOT" + this.photonView.ViewID;
            }
            else
            {
                actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
                nickname = PhotonNetwork.LocalPlayer.NickName;
                SetLayer(7); // Set to localplayer layer
            }
            isReady = true;
        }

        // Announce self to GM.
        lm.Log(logSrc, "Started. Announcing to GameManager.");
        gm.AddPlayer(this);

        // Parent self to gm's spawned-players object for cleanliness
        this.transform.parent = gm.playerSpawnParent;
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
            stream.SendNext(actorNumber);
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
            this.actorNumber = (int)stream.ReceiveNext();
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
            Transform newTransform = gm.GetPlayerSpawnLocation();
            this.transform.position = newTransform.position;
            this.transform.rotation = newTransform.rotation;
            if (charConWasEnabled) charCon.enabled = true;
        }
        oil = 100;
        maxOil = 100;
        // Set damage of each BodyPart to 0
        foreach(BodyPart bp in bodyParts) { bp.Damage = 0; }
        IsDead = false;
        this._role = gm.GetRoleFromID(0);

        SetRagdoll(false);
    }

    [PunRPC]
    public void TakeDamage(int dmg, string bodyPartName)
    {
        // Don't deal with damage if we don't own this player
        //if (!photonView.IsMine) return; // TEMPORARILY do this on all clients for the visuals, need proper BodyPart syncing

        // Can't take more damage if we're dead
        if (IsDead) return;

        // Play damage sound if we own this player
        if (photonView.IsMine) audioSrc.PlayOneShot(damageSound);

        // Find the BodyPart that took damage
        BodyPart bodyPart = GetBodyPartByName(bodyPartName);
        if (bodyPart)
        {
            bodyPart.Damage += dmg;
        } else
        {
            lm.LogError(logSrc, $"Could not find body part '{bodyPartName}'");
        }

        // To make damage more responsive, kill player instantly if damage > oil
        if (GetDamage() > oil)
        {
            Kill();
        }
    }

    public void Heal(int amount)
    {
        // Run loop once for each heal amount
        int healedParts = 0;
        for (int i = 0; i < amount; i++)
        {
            // Get a bodypart with damage
            BodyPart bp = bodyParts.FirstOrDefault(bp => bp.Damage > 0);
            if (!bp) break;
            // Reduce damage by 1
            bp.Damage--;
            healedParts++;
        }

        // Do visuals if we healed anything
        if (healedParts > 0) photonView.RPC("DoHealVisuals", RpcTarget.All);
    }

    void DoFootstep(float volumeMultiplier)
    {
        // Cast a ray down at the thing under our feet
        bool hit = Physics.Raycast(this.transform.position, Vector3.down, out RaycastHit footstepHit, 1f);
        if (!hit) return;
        audioSrc.PlayOneShot(gm.GetFootstepByMaterial(footstepHit.collider.material), fFootstepVolume * volumeMultiplier);
    }

    // Called on all clients by the fpscontroller
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
        if (audioSrc && healSound) audioSrc.PlayOneShot(healSound);
    }

    // Ticks happen once a second. Why though? Is it so we reduce network sync traffic?
    // It would be cleaner to ignore this and round values for the UI.
    private float msSinceLastTick = 0;
    public void Update()
    {
        // Footsteps
        if (IsGrounded && isRunning)
        {
            sLastFootstep += Time.deltaTime;
            if (sLastFootstep > footstepInterval)
            {
                sLastFootstep = 0f;
                DoFootstep(1);
            }
        } else
        {
            // If not moving, set interval to half a footstep so our first step comes a little quicker
            sLastFootstep = footstepInterval / 2;
        }

        if (!photonView.IsMine) return;

        // Track healing time
        if (isHealing && !IsDead)
        {
            sSinceLastHeal += Time.deltaTime;
            if (sSinceLastHeal > 1)
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
                audioSrc.PlayOneShot(damageSound);
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
            bpData[i] = bodyParts[i].gameObject.name + ":" + bodyParts[i].Damage;
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
            string[] split = part.Split(':');
            // Find a BodyPart by name of split[0]
            
            BodyPart bp = GetBodyPartByName(split[0]);
            if (!bp)
            {
                lm.LogError(logSrc, $"Error deserialising BodyPart damage, invalid part name. Data: '{data}'");
                return;
            }
            bp.Damage = int.Parse(split[1]);
        }
    }

    public int GetDamage()
    {
        return bodyParts.Sum(bp => bp.Damage);
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

        // Find an Animator and turn it off
        Animator ani = GetComponent<Animator>();
        if (ani) ani.enabled = !ragdoll;
    }

    // Instantly kills this player (if local)
    public void Kill()
    {
        if (!photonView.IsMine)
        {
            lm.LogError(logSrc, "Tried to Kill a non-local player");
            return;
        }
        IsDead = true;
    }

    // Called when when isdead is set to true
    void Die()
    {
        lm.Log(logSrc, $"{nickname} died.");

        SetRagdoll(true);

        // We don't need to do anything else if this isn't our player
        if (!this.photonView.IsMine) return;

        // Play death sound
        audioSrc.PlayOneShot(deathSound);

        if (!isBot) gm.Alert("YOU DIED");
        // TODO this might be better hooking into an event to increase modularity
        // Try to drop our held item
        FpsController fpsc = GetComponent<FpsController>();
        if (fpsc) fpsc.TryDropHeldItem();

        // Dont do any UI stuff for bots
        if (!isBot)
        {
            // Turn on the dead screen
            gm.DeadScreen.SetActive(true);
            // Set dead screen message
            //gm.DeathDetailsText.text = $"Killed by {source} with {method}";
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
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Gun : MonoBehaviourPun, IPunObservable
{
    // Things we need for a gun:
    // Time between shots (everything is full auto, even a sniper)
    public float timeUntilNextShot = 0f;
    public float timeBetweenShots = 0.5f;
    // Shots in mag
    public int bulletsInMag = 10;
    // Mag size
    public int magazineSize = 10;

    // Reload time
    public float reloadTime = 2f;
    // is currently reloading
    bool isReloading;
    // Time until reload is done
    float timeUntilReloaded;

    // is trigger being held down, set by HeldItem
    public bool triggerDown = false;
    // Angle around which recoil is applied to character/camera. This is right for most guns, which recoil upwards.
    Vector3 recoilVector = Vector3.right;
    // Aim vector, set by HeldItem
    public Vector3 aimVector;
    // Origin point set by HeldItem
    public Vector3 aimOrigin;
    // Axis around which recoil turns the gun
    public Vector3 recoilAxis;
    // Decal prefab
    public GameObject decalPrefab;
    // Current recoil (on the x axis for now)
    public float currentRecoil = 0f; // Degrees
    // Max recoil
    public float maxRecoil = 40f; // Degrees
    // TODO accuracy
    // Recoil increase per shot
    public float recoilPerShot = 10f; // Degrees
    // Recoil decrease per second
    public float recoilPerSecond = -20f;
    // Shot damage
    public int damage = 1;
    // Shot force (physics)
    public float shotForce = 200f;
    // TODO Rays per shot (1 for most guns, more for shotguns) (need accuracy)
    // Shot range
    public float range = 100f;
    // Shot sound
    public AudioSource shotSound;
    // Reload sound
    public AudioSource reloadSound;
    // Ref to camera of the person holding this gun, so we can add recoil to the camera
    public Camera cam;
    // Ref to the model anchor transform, which is rotated in some situations (eg, mac10 held sideways)
    [SerializeField]
    GameObject modelAnchor;
    // Does gun support being held sideways? 
    public bool supportsSideways = true;

    [PunRPC]
    public void Setup(int playerId)
    {
        Player owner = GameManager.gm.GetPlayerByID(playerId);
        if (!owner)
        {
            Debug.LogError("Gun setup could not find player " + playerId);
            return;
        }


        this.transform.parent = owner.itemAnchor.transform;
        this.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        owner.heldItem = this.gameObject;
        owner.heldItemScript = owner.heldItem.GetComponent<HeldItem>();
    }

    // Called by the shooter on all other clients
    [PunRPC]
    public void RpcDoSoundAndVisuals(bool localOverride, Vector3 origin, Vector3 forward)
    {
        // Don't do this if we are the owner. This method will have already been called locally to avoid lag.
        if (photonView && photonView.IsMine && !localOverride) return;

        // Play shoot sound
        shotSound.Play();

        // Declare a raycast hit to store information about what our raycast has hit
        RaycastHit hit;
        // Check if our raycast has hit anything
        if (Physics.Raycast(origin, forward, out hit, range))
        {
            // TODO pool objects
            // TODO network instantiate or send rpc to clients?
            //Spawn the decal object just above the surface the raycast hit
            GameObject decalObject = Instantiate(decalPrefab, hit.point + (hit.normal * 0.025f), Quaternion.FromToRotation(decalPrefab.transform.up, hit.normal)) as GameObject;
            //Rotate the decal object so that it's "up" direction is the surface the raycast hit
            //decalObject.transform.rotation = Quaternion.FromToRotation(hit.normal, decalPrefab.transform.up);
            // Parent the decal object to the hit gameobject so it can move around
            //decalObject.transform.parent = hit.transform;
        }
    }

    // (requiresAuthority = false)
    // Run on the server, initiated by client when shooting a gun
    //[Command]
    void CmdShoot(Vector3 origin, Vector3 forward)
    {
        RpcDoSoundAndVisuals(true, origin, forward);
        // Tell clients to do audio/visual stuff
        photonView.RPC("RpcDoSoundAndVisuals", RpcTarget.Others, false, origin, forward);

        // Declare a raycast hit to store information about what our raycast has hit
        RaycastHit hit;
        // Check if our raycast has hit anything
        if (Physics.Raycast(origin, forward, out hit, range))
        {
            // Tell the object we hit to take damage
            hit.transform.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            // Check if the object we hit has a rigidbody attached
            if (hit.rigidbody != null)
            {
                // Add force to the rigidbody we hit, in the direction from which it was hit
                hit.rigidbody.AddForce(-hit.normal * shotForce);
            }
        }

    }

    // Attempt to shoot, called when left-clicking
    void Shoot()
    {
        // Only do this for the local player (seems to never work)
        //if (!isLocalPlayer) return;

        // Add time between shots
        timeUntilNextShot += timeBetweenShots;

        // Remove bullet from mag
        bulletsInMag--;

        // Check we have an aim vector
        if (aimVector == null || aimOrigin == null || recoilAxis == null)
        {
            Debug.LogError("[Gun] " + name + " has no aim vector/origin/recoilAxis");
            return;
        }

        // Create a vector at the center of our camera's viewport
        //Vector3 rayOrigin = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0.0f));

        // Tell server we're shooting
        // Add recoil to our aim vector
        //Vector3 recoilVector = Quaternion.AngleAxis(-currentRecoil, recoilAxis) * aimVector;
        CmdShoot(aimOrigin, aimVector);

        // Add recoil
        //currentRecoil += recoilPerShot;
        //if (currentRecoil > maxRecoil) currentRecoil = maxRecoil;

        // Add recoil to camera/character
        //cam.transform.Rotate(transform.right, recoilPerShot);
        cam.transform.Rotate(Vector3.right, -recoilPerShot * recoilVector.normalized.x); // HEY this won't work because cam is fixed to y axis. We need to rotate character instead
        // So how do we do that? Any y rotation goes to character, and x rotation goes to camera. Split out euler angles of gun?
        cam.transform.parent.Rotate(Vector3.up, -recoilPerShot * recoilVector.normalized.y);


        // Debug line
        //RaycastHit hit;
        //if (Physics.Raycast(aimOrigin, aimVector, out hit, range))
        //{
        //    LineRenderer laserLine = GetComponent<LineRenderer>();
        //    if (laserLine)
        //    {
        //        // Set the end position for our laser line 
        //        laserLine.SetPosition(1, hit.point);
        //        // Set the start position for our visual effect for our laser to the position of our gun end
        //        laserLine.SetPosition(0, aimOrigin);
        //    }
        //}
    }

    // Called when the player hits the reload key
    void StartReload()
    {
        if (isReloading) return;
        isReloading = true;
        timeUntilReloaded = reloadTime;
        reloadSound.Play();
        // Set downward angle, which cheaply indicates we're reloading
        modelAnchor.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

    }

    // Called when a reload finishes
    private void FinishReload()
    {
        timeUntilReloaded = 0f;
        isReloading = false;
        // Reload bullets in mag
        bulletsInMag = magazineSize;
        // Remove downward angle
        modelAnchor.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    // Update is called once per frame
    void Update()
    {
        // Hold gun sideways if it supports it. Sets recoil vector as well;
        // Can't do this when reloading
        if (modelAnchor && supportsSideways && !isReloading)
        {
            float rot;
            if (Input.GetMouseButton(1))
            {
                rot = 90f;
                recoilVector = Vector3.up;
            } else
            {
                rot = 0f;
                recoilVector = Vector3.right;
            }
            modelAnchor.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        if (isReloading)
        {
            // Reduce time to reload
            timeUntilReloaded -= Time.deltaTime;
            // check if reloaded
            if (timeUntilReloaded < 0f)
            {
                // We have finished a reload
                FinishReload();
            }
        }

        // Reduce recoil
        currentRecoil -= recoilPerSecond * Time.deltaTime;
        if (currentRecoil < 0f) currentRecoil = 0f;

        // Reduce time needed for next shot
        timeUntilNextShot -= Time.deltaTime;
        if (timeUntilNextShot < 0f) timeUntilNextShot = 0f;

        if (Input.GetKeyDown("r")) // TODO this won't work in multiplayer
        {
            StartReload();
        }

        if (triggerDown && timeUntilNextShot == 0f && bulletsInMag > 0 && !isReloading)
        {
            Shoot();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(bulletsInMag);
        }
        else
        {
            this.bulletsInMag = (int)stream.ReceiveNext();
        }
    }
}

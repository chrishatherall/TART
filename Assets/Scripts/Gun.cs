using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;

// TODO recoil/firing inaccuracy

public class Gun : MonoBehaviourPun
{
    // Model recoil while shooting
    // Save the default model and max-recoil position (which will be Vector3.zero)
    // When shooting, we'll lerp between these two, based on firing time.
    public Vector3 minRecoilPosition = Vector3.zero; // Could change these to transforms to also hold a rotation?
    public Vector3 maxRecoilPosition;

    // Time between shots (everything is full auto, even a sniper)
    public float timeUntilNextShot = 0f;
    public float timeBetweenShots = 0.5f;
    // Shots currently in mag
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
    // Ref to fpscontoller of the person holding this gun, so we can add recoil to the camera via it
    public FpsController fpsController;
    // Ref to the camera, which we use for aiming
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
            gm.LogError("Gun setup could not find player " + playerId);
            return;
        }

        this.transform.parent = owner.itemAnchor.transform;
        this.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        owner.heldItem = this.gameObject;
        owner.heldItemScript = owner.heldItem.GetComponent<HeldItem>();

        fpsController = GetComponentInParent<FpsController>();
        cam = fpsController.playerCamera;
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
            //Spawn the decal object just above the surface the raycast hit
            GameObject decalObject = Instantiate(decalPrefab, hit.point + (hit.normal * 0.025f), Quaternion.FromToRotation(decalPrefab.transform.up, hit.normal)) as GameObject;
            // Parent the decal object to the hit gameobject so it can move around
            decalObject.transform.parent = hit.transform;
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
        if (aimOrigin == null)
        {
            gm.LogError("[Gun] " + name + " has no aim origin");
            return;
        }

        // Tell ourselves to do the visuals immediately to avoid lag
        RpcDoSoundAndVisuals(true, aimOrigin, cam.transform.forward);
        // Tell clients to do audio/visual stuff
        photonView.RPC("RpcDoSoundAndVisuals", RpcTarget.Others, false, aimOrigin, cam.transform.forward);

        // Declare a raycast hit to store information about what our raycast has hit
        RaycastHit hit;
        // Check if our raycast has hit anything
        if (Physics.Raycast(aimOrigin, cam.transform.forward, out hit, range))
        {
            // Tell the object we hit to take damage
            // TODO needs to be RPCd
            hit.transform.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            // Check if the object we hit has a rigidbody attached
            if (hit.rigidbody != null)
            {
                // TODO not networked
                // Add force to the rigidbody we hit, in the direction from which it was hit
                hit.rigidbody.AddForce(-hit.normal * shotForce);
            }
        }

        // Add recoil to camera/character
        if (fpsController)
        {
            fpsController.CameraWiggle = recoilPerShot;
        } else
        {
            gm.LogError("[Gun] Cannot find fps_controller to add camera wiggle");
        }
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
        // Move our gun when fired. Doesn't affect aim, just visual.
        float lerpAmount = timeUntilNextShot / timeBetweenShots;
        this.transform.localPosition = Vector3.Lerp(minRecoilPosition, maxRecoilPosition, lerpAmount);

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

}

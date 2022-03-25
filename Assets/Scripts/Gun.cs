using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using static GameManager;
using static LogManager;

public class Gun : MonoBehaviourPun
{
    readonly string logSrc = "GUN";

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

    float bulletSpeed = 30; // ms/s, used for trail renderer NOT hit detection

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
    public GameObject barrelEnd;
    // Axis around which recoil turns the gun
    public Vector3 recoilAxis;
    // Decal prefab
    public GameObject decalPrefab;
    // Current recoil
    public float currentRecoil = 0f; // Degrees
    // Max recoil
    public float maxRecoil = 40f; // Degrees
    // Recoil increase per shot
    public float recoilPerShot = 10f; // Degrees
    // Recoil decrease per second
    public float recoilPerSecond = -20f;
    // Shot damage
    public int damage = 1;
    // Shot force (physics)
    public float shotForce = 20f;
    // TODO Rays per shot (1 for most guns, more for shotguns) (need accuracy)
    // Shot range
    public float range = 100f;
    // Shot sound
    public AudioSource shotSound;
    // Reload sound
    public AudioSource reloadSound;
    // Ref to Player holding this gun, so we can add recoil to the camera via it
    public Character character;
    // Ref to the camera, which we use for aiming
    public Camera cam;

    // Ref to the model anchor transform
    [SerializeField]
    GameObject modelAnchor;
    // Ref to our default trail renderer
    [SerializeField]
    TrailRenderer DefTrail;
    // Ref to our player owner
    Character weilder;
    // Ref to our own helditem script
    HeldItem heldItemScript;

    void Awake()
    {
        heldItemScript = this.GetComponent<HeldItem>();
    }

    // Called by the HeldItem script when the owner is set
    public void SetOwner(int ownerCharacterId)
    {
        weilder = GameManager.gm.GetCharacterById(ownerCharacterId);
        if (!weilder)
        {
            lm.LogError(logSrc,"Gun setup could not find character " + ownerCharacterId);
            return;
        }

        this.transform.parent = weilder.itemAnchor.transform;
        this.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        this.transform.localPosition = Vector3.zero;
        weilder.heldItem = this.gameObject;
        weilder.heldItemScript = heldItemScript;

        // If we're setting up for ourselves, set audio emitter to 2d so the gun firing noise
        // doesn't annoyingly favour one ear.
        if (photonView.IsMine)
        {
            character = GetComponentInParent<Character>();
            cam = character.Camera;
            GetComponent<AudioSource>().spatialBlend = 0f;
        }
    }

    // Called by the shooter on all other clients
    [PunRPC]
    public void RpcDoSoundAndVisuals(Vector3 origin, Vector3 direction)
    {
        // Play shoot sound
        shotSound.PlayOneShot(shotSound.clip);

        // Temp thing to try and not hit ourselves
        int layermask = 1;
        if (photonView.IsMine) layermask = ~(1 << 7);

        RaycastHit hit;
        Vector3 trailEndPoint;
        bool hitSomething = Physics.Raycast(origin, direction, out hit, range, layermask);
        // Check if our raycast has hit anything
        if (hitSomething) // TODO for other people this will likely hit our player all the time
        {
          trailEndPoint = hit.point;
          // TODO pool objects
          //Spawn the decal object just above the surface the raycast hit
          GameObject decalObject = Instantiate(decalPrefab, hit.point + (hit.normal * 0.025f), Quaternion.FromToRotation(decalPrefab.transform.up, hit.normal)) as GameObject;
          // Parent the decal object to the hit gameobject so it can move around
          decalObject.transform.parent = hit.transform;

          // TODO probably not the best place for this but it works ish
          // Check if the object we hit has a rigidbody attached
          if (hit.rigidbody != null)
          {
            // Add force to the rigidbody we hit, in the direction from which it was hit
            hit.rigidbody.AddForce(-hit.normal * shotForce, ForceMode.Impulse);
          }
        } else {
          // Didn't hit anything, so set the end point of the trail renderer somewhere forward.
          // TODO
          trailEndPoint = Vector3.zero;
        }

        // Spawn a new bullet tracer and start routine to move it
        TrailRenderer trail = Instantiate(DefTrail, barrelEnd.transform.position, Quaternion.identity);
        StartCoroutine(MoveTrail(trail, trailEndPoint));
    }

    IEnumerator MoveTrail(TrailRenderer trail, Vector3 endLocation) {
        float time = 0;
        Vector3 startLocation = trail.transform.position;
        float travelTime = Vector3.Distance(startLocation, endLocation) / bulletSpeed;

        while (time < travelTime) {
            trail.transform.position = Vector3.Lerp(startLocation, endLocation, time);
            time += Time.deltaTime / trail.time;
            yield return null;
        }
        trail.transform.position = endLocation;
        Destroy(trail.gameObject, trail.time);
    }

    // Attempt to shoot, called when left-clicking
    void Shoot()
    {
        // Add time between shots
        timeUntilNextShot += timeBetweenShots;

        // Remove bullet from mag
        bulletsInMag--;

        // Check we have an aim vector
        if (aimOrigin == null)
        {
            lm.LogError(logSrc,name + " has no aim origin");
            return;
        }

        // Determine bullet direction
        // Add weapon inaccuracy. Recoil goes up in a V
        Vector3 bulletDirection = cam.transform.forward +
            Quaternion.AngleAxis(currentRecoil, cam.transform.up).ToEuler() +
            Quaternion.AngleAxis(Random.Range(-50f, 51f)/100f * currentRecoil, cam.transform.right).ToEuler();

        // Tell clients (including ourselves) to do audio/visual stuff
        photonView.RPC("RpcDoSoundAndVisuals", RpcTarget.All, aimOrigin, bulletDirection);

        // Check if our raycast has hit anything
        if (Physics.Raycast(aimOrigin, bulletDirection, out RaycastHit hit, range, ~(1 << 7))) // TODO move layermask declaration
        {
            // Tell the object we hit to take damage
            // We currently only damage bodyparts
            BodyPart bp = hit.transform.GetComponent<BodyPart>();
            if (bp)
            {
                // If we hit a bodypart, send an rpc to the parent player object
                bp.TakeDamage(damage, bulletDirection * shotForce, weilder.ID, heldItemScript.nickname);
            }

            // TODO try a sendmessage(takedamage) instead


        }

        // Add recoil to gun
        currentRecoil += recoilPerShot;

        // Add recoil to camera/character
        if (character)
        {
            character.CameraWiggle = recoilPerShot /2;
        } else
        {
            lm.LogError(logSrc,"Cannot find FpsController to add camera wiggle");
        }
    }

    void Reload()
    {
        photonView.RPC("StartReload", RpcTarget.All);
    }

    [PunRPC]
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
        } else
        {
            // Move our gun when fired. Doesn't affect aim, just visual.
            float lerpAmount = timeUntilNextShot / timeBetweenShots;
            modelAnchor.transform.localPosition = Vector3.Lerp(minRecoilPosition, maxRecoilPosition, lerpAmount);
            // Also needs to rotate along with recoil
            modelAnchor.transform.localRotation = Quaternion.Euler(-currentRecoil, 0f, 0f);
        }

        // Reduce recoil
        currentRecoil -= recoilPerSecond * Time.deltaTime;
        if (currentRecoil < 0f) currentRecoil = 0f;

        // Reduce time needed for next shot
        timeUntilNextShot -= Time.deltaTime;
        if (timeUntilNextShot < 0f) timeUntilNextShot = 0f;

        if (triggerDown && timeUntilNextShot == 0f && bulletsInMag > 0 && !isReloading)
        {
            Shoot();
        }
    }

}

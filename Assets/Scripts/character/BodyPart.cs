using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LogManager;
using TART;

// Tells a parent Player script when we're damaged, and stores damage values. Controls a particle system to show damage.
public class BodyPart : MonoBehaviour, IDamageTaker
{
    readonly string logSrc = "BodyPart";

    // Ref to our parent Player script
    public Player p;

    // Cumulative force
    public Vector3 cumulativeForce = new Vector3();
    // Time in which we'll clear our force
    float timeToForceClear = 0f;
    // Default time between clearing force
    float timeBetweenForceClear = 0.1f;

    // The list of Damages on this body part
    List<Damage> _damages;
    public List<Damage> Damages { get => _damages; }

    // The sum of all Damages on this part. Calculated after a change to the Damage list.
    [SerializeField]
    int _currentDamage = 0;

    // The particle system we turn on when damaged. Needs to be manually set
    [SerializeField]
    ParticleSystem ps;

    // Ref to the collider
    new public Collider collider;

    // The last deserialisation string we received. If we receive the same string, we skip deserialisation.
    string _lastDeserialisationString;

    // Read-only access to the current damage
    public int CurrentDamage { 
        get => _currentDamage;
    }


    private void Update()
    {
        // Control particle system
        if (!ps) return;
        if (ps.gameObject.activeSelf)
        {
            // Find reasons to disable ps
            if (_currentDamage == 0 || p.oil <= 0) ps.gameObject.SetActive(false);
        } else
        {
            // Find reasons to enable ps
            if (_currentDamage > 0 && p.oil > 0) ps.gameObject.SetActive(true);
        }

        // Clear force
        if (timeToForceClear > 0f)
        {
            timeToForceClear -= Time.deltaTime;
            if (timeToForceClear < 0f) cumulativeForce = new Vector3();
        }
    }

    void Awake()
    {
        _damages = new List<Damage>();
        p = GetComponentInParent<Player>();
        if (!p) lm.LogError(logSrc, $"{this.name} could not find parent Player. This WILL cause errors!");
        collider = GetComponent<Collider>();
        // Warn if ps isn't set. Add bone name.
        if (!ps) lm.LogError(logSrc, "Missing particle system reference on " + this.name);
    }

    // Should only be called from the local Player
    public void AddDamage(int dmg, int sourcePlayerID, string sourceWeapon)
    {
        // Local only
        if (!p.photonView.IsMine)
        {
            lm.LogError(logSrc, $"AddDamage called on {this.name} from non-owner");
            return;
        }
        // TODO if we already have a Damage from this player and source, increase it

        _damages.Add(new Damage(sourcePlayerID, dmg));

        // Recalculate _damage
        CalculateCurrentDamage();
    }

    // Should only be called from the local Player
    public void RemoveDamage(int dmg)
    {
        // Local only
        if (!p.photonView.IsMine)
        {
            lm.LogError(logSrc, $"RemoveDamage called on {this.name} from non-owner");
            return;
        }

        // TODO

        // Recalculate _damage
        CalculateCurrentDamage();
    }

    // Removes all damage, called on fresh spawn
    public void Reset()
    {
        if (_damages != null) _damages.Clear();
        CalculateCurrentDamage();
    }

    void CalculateCurrentDamage()
    {
        _currentDamage = 0;
        foreach (Damage D in _damages)
        {
            _currentDamage += D.Amount;
        }
    }

    public string Serialise()
    {
        // bp-head:1001:4:1002:8
        string serial = this.name;
        foreach (Damage D in _damages)
        {
            serial += $":{D.SourcePlayerID}:{D.Amount}";
        }

        if (p.IsDead)
        {
            // Add position
            serial += $";{transform.position.x}:{transform.position.y}:{transform.position.z}";
            // Add rotation
            serial += $":{transform.rotation.x}:{transform.rotation.y}:{transform.rotation.z}:{transform.rotation.w}";
        }
        return serial;
    }

    public void Deserialise(string serial)
    {
        if (serial == _lastDeserialisationString) return;
        _lastDeserialisationString = serial;

        _damages.Clear();

        // Split serial into damage string and position string
        string[] split = serial.Split(';');

        // Damage string
        string[] damageStr = split[0].Split(':');
        // bp-head:1001:4:1002:8
        for (int i = 1; i < damageStr.Length; i+=2)
        {
            _damages.Add(new Damage(damageStr[i], damageStr[i + 1]));
        }
        CalculateCurrentDamage();

        // Position string
        if (split.Length > 1)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            bool wasKinematic = rb.isKinematic;
            bool wasGravity = rb.useGravity;
            rb.isKinematic = true;
            rb.useGravity = false;

            string[] positionStr = split[1].Split(':');
            transform.position = new Vector3(
                float.Parse(positionStr[0]),
                float.Parse(positionStr[1]),
                float.Parse(positionStr[2])
                );
            transform.rotation = new Quaternion(
                float.Parse(positionStr[3]),
                float.Parse(positionStr[4]),
                float.Parse(positionStr[5]),
                float.Parse(positionStr[6])
                );

            rb.isKinematic = wasKinematic;
            rb.useGravity = wasGravity;
        }
    }


    // This is an object that can receive damage.
    // Note: This method is always local to the damage dealer, we need to forward damage onto the player script via rpc.
    public void TakeDamage(int dmg, Vector3 hitDirection, int sourcePlayerID, string sourceWeapon)
    {
        // Also sends bone (object) name, so the Player knows which part of the body took damage
        if (p) p.photonView.RPC("DamageBone", Photon.Pun.RpcTarget.All, this.name, dmg, hitDirection, sourcePlayerID, sourceWeapon);
    }

    public void AddForce(Vector3 hitDirection)
    {
        // Add this force to our current force
        cumulativeForce += hitDirection;
        // Set timer to clear force
        timeToForceClear = timeBetweenForceClear;
    }

}

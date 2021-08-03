using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class RotatingDoor : MonoBehaviourPun, IPunObservable
{
    public enum DoorState
    {
        Closed,
        Closing,
        Opened,
        Opening,
        Halted // Open a bit but stopped due to collision
    }

    public enum RotateAxis
    { X, Y }

    public enum DoorSwing
    { OneWay, BothWays } // Some doors can only open forward, some can open both ways

    public RotateAxis Axis = RotateAxis.Y; // Doors are Y by default
    Vector3 rAxis;

    public DoorSwing OpenDirection = DoorSwing.BothWays;

    // Current state of the door. Default is closed
    public DoorState state = DoorState.Closed;

    // This is how much the door can rotate from closed to open
    public float maxOpenAngle = 90f;

    // Open direction modifier. 1 for clockwise, -1 for counterclockwise. Used when opening/closing, set when 
    // door is activated by determining which way it should rotate.
    int rotateDirection;

    // Ref to the audio player
    AudioSource audioSource;

    // On activate, if it isn't closed then try to close
    // If it's closing/opening, switch it around
    // if it's closed, try to open in the right direction somehow. Compare hit point to local x?

    // when rotating, if we collide with anything then stop
    // If we hit a limit (max rotation if opening, 0 if closing) then set limit and stop

    private void Start()
    {
        // Find audio source
        audioSource = GetComponent<AudioSource>();

        // Set rotate axis
        switch (Axis)
        {
            case RotateAxis.X:
                rAxis = Vector3.right; // Vents
                break;
            case RotateAxis.Y:
                rAxis = Vector3.up; // Doors
                break;
        }
    }

    private float CurrentAngle()
    {
        Vector3 angles = Vector3.Scale(this.transform.localEulerAngles, rAxis);
        return angles.x + angles.y + angles.z;
    }

    // Update is called once per frame
    void Update()
    {
        switch (state)
        {
            case DoorState.Opening:
                // Rotate the door by speedXdirection
                this.transform.RotateAroundLocal(rAxis, rotateDirection * 5f * Time.deltaTime);
                // Check for rotation limit
                float cAngle = CurrentAngle();
                
                if (cAngle < 180f && cAngle > maxOpenAngle)
                {
                    this.transform.localEulerAngles = new Vector3(maxOpenAngle * rAxis.x, maxOpenAngle * rAxis.y, 0f);
                    state = DoorState.Opened;
                }
                if (cAngle > 180f && cAngle < 360f-maxOpenAngle)
                {
                    this.transform.localEulerAngles = new Vector3(maxOpenAngle * rAxis.x, -maxOpenAngle * rAxis.y, 0f);
                    state = DoorState.Opened;
                }
                break;

            case DoorState.Closing:
                // Rotate the door by speedXdirectionToclose
                this.transform.RotateAroundLocal(rAxis, rotateDirection * 5f * Time.deltaTime); // Use the opposite direction of the last opening direction
                // Check for rotation limit
                if (CurrentAngle() < 3f) // Difference between current angle and closed angle
                {
                    this.transform.localEulerAngles = Vector3.zero;
                    state = DoorState.Closed;
                }
                break;

            default:
                break;
        }

    }

    private void OnCollisionEnter(Collision collision) // TODO never seems to work?
    {
        if (state == DoorState.Opening || state == DoorState.Closing)
        {
            state = DoorState.Halted;
        }
    }


    // Called by Activatable script on all connected clients
    public void OnActivated(Vector3 position)
    {
        // Play activate audio
        if (audioSource) audioSource.Play();

        // Control door logic only if we're the server
        if (!photonView.IsMine) return;
        if (state == DoorState.Closed || state == DoorState.Closing)
        {
            state = DoorState.Opening;
            if (OpenDirection == DoorSwing.BothWays)
            {
                // Determine direction of the door opening by using the hit position z
                // Position is the WORLD position and we want the local one
                Vector3 localPos = transform.InverseTransformPoint(position);
                rotateDirection = (localPos.z > 0) ? 1 : -1; // TODO only works with vertical hinge?
            }
            else
            {
                // If the door only opens one way, the direction is always positive
                rotateDirection = 1;
            }
            return;
        }

        if (state == DoorState.Opened || state == DoorState.Opening || state == DoorState.Halted)
        {
            state = DoorState.Closing;
            rotateDirection = (CurrentAngle() < 180f) ? -1 : 1;
            return;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(state);
            stream.SendNext(rotateDirection);
            stream.SendNext(this.transform.rotation);
        }
        else
        {
            this.state = (DoorState)stream.ReceiveNext();
            this.rotateDirection = (int)stream.ReceiveNext();
            this.transform.rotation = (Quaternion)stream.ReceiveNext();
        }
    }
}

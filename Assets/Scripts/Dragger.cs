using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Helper script to aid a character in dragging items

public class Dragger : MonoBehaviour
{
    float spring = 1000f;
    float damper = 100f;
    float breakForce = 400f;
    public SpringJoint joint;
    public Draggable draggingObject;

    private void OnJointBreak(float breakForce)
    {
        // Tell character to stop dragging
        SendMessageUpwards("StopDraggingItem", SendMessageOptions.DontRequireReceiver);
    }

    public void EnsureJoint ()
    {
        if (joint) return;
        joint = gameObject.AddComponent<SpringJoint>();
        joint.spring = spring;
        joint.damper = damper;
        joint.breakForce = breakForce;
    }

    public void PickUp(Draggable d)
    {
        // Make sure the dragger has a joint
        EnsureJoint();
        // This draggable is now the item we're dragging
        draggingObject = d;
        // Set our dragger at the hit position
        // TODO lastHit won't be updated for clients who aren't controlling this character, but we could use the aim vector
        transform.position = d.transform.position; //lastHit.transform.position;
        joint.connectedBody = d.rb;
    }

    public void Drop()
    {
        // Our joint could be broken
        if (joint && joint.connectedBody)
        {
            // Give the item a little nudge to wake up the physics engine.
            joint.connectedBody.AddForce(new Vector3(0f, 0.0001f));
            joint.connectedBody = null;
        }
        // We are no longer dragging this item
        draggingObject = null;
    }
}

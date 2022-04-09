using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TartTransformView : MonoBehaviourPunCallbacks, IPunObservable
{
    private float m_Angle;

    private Vector3 m_Direction;
    private Vector3 m_OldNetworkPosition;
    private Vector3 m_NetworkPosition;
    private Vector3 m_StoredPosition;

    private Quaternion m_NetworkRotation;

    private float syncWindow = 0.1f;

    public bool m_SynchronizePosition = true;
    public bool m_SynchronizeRotation = true;

    bool m_firstTake = true;

    public void Awake()
    {
        m_StoredPosition = transform.position;
        m_OldNetworkPosition = Vector3.zero;
        m_NetworkPosition = Vector3.zero;
        m_NetworkRotation = Quaternion.identity;

        syncWindow = 1f / PhotonNetwork.SerializationRate;
    }

    public void Update()
    {
        var tr = transform;

        if (!this.photonView.IsMine)
        {
            // Need to use either the old network position or current physical position, whichever is further away.
            float dist = Mathf.Max(
                Vector3.Distance(m_OldNetworkPosition, m_NetworkPosition),
                Vector3.Distance(tr.position, m_NetworkPosition)
                );
            // This doesn't work if we're very fast then very slow, as a character wildly out of position won't ever catch up
            // float mSpeed = dist;// * (1f / PhotonNetwork.SerializationRate);
            
            // To smooth this nicely we need the last 2 network positions. The difference * 100/SerializationRate is our speed.
            // The max distance is speed * Time.deltaTime, but set min speed to ~0.1f

            // This is how it would work with no smooth time. Need to add a very small amount of smoothing.
            tr.position = Vector3.MoveTowards(tr.position, this.m_NetworkPosition, dist * Time.deltaTime * (1/syncWindow)); // * (1f / PhotonNetwork.SerializationRate));
            
            // arbitrary 3 times faster than current shambles of an implementation
            tr.rotation = Quaternion.RotateTowards(tr.rotation, this.m_NetworkRotation, this.m_Angle * (1f / PhotonNetwork.SerializationRate) * 3f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        var tr = transform;

        // Write
        if (stream.IsWriting)
        {
            if (this.m_SynchronizePosition)
            {
                this.m_Direction = tr.position - this.m_StoredPosition;
                this.m_StoredPosition = tr.position;
                stream.SendNext(this.m_StoredPosition);
                stream.SendNext(this.m_Direction);
            }

            if (this.m_SynchronizeRotation)
            {
                stream.SendNext(tr.rotation);
            }
        }
        // Read
        else
        {
            if (this.m_SynchronizePosition)
            {
                this.m_OldNetworkPosition = this.m_NetworkPosition;
                this.m_NetworkPosition = (Vector3)stream.ReceiveNext();
                this.m_Direction = (Vector3)stream.ReceiveNext();

                if (m_firstTake)
                {
                    tr.position = this.m_NetworkPosition;
                }
                else
                {
                    float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
                    this.m_NetworkPosition += this.m_Direction * lag;
                }

            }

            if (this.m_SynchronizeRotation)
            {
                this.m_NetworkRotation = (Quaternion)stream.ReceiveNext();

                if (m_firstTake)
                {
                    this.m_Angle = 0f;
                    tr.rotation = this.m_NetworkRotation;
                }
                else
                {
                    this.m_Angle = Quaternion.Angle(tr.rotation, this.m_NetworkRotation);
                }
            }

            if (m_firstTake)
            {
                m_firstTake = false;
            }
        }
    }
}

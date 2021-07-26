using Photon.Realtime;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExitGames.Client.Photon;
using static GameManager;

public class ItemSpawn : MonoBehaviour, IOnEventCallback
{
    [SerializeField]
    string spawnList; // This should match the name of the Object containing SpawnListItem components.

    // Listen for round start to spawn a random item
    public void OnEvent(EventData photonEvent)
    {
        int eventCode = photonEvent.Code;
        Debug.Log(eventCode);

        if (eventCode == (int)Events.RoundStart && PhotonNetwork.IsMasterClient)
        {
            SpawnItem();
        }
    }

    public void Start()
    {
        if (PhotonNetwork.IsMasterClient) SpawnItem();
    }

    void SpawnItem()
    {
        // Find a random item from our list
        GameObject prefab = gm.GetItemFromSpawnList(spawnList);
        Debug.Log("Spawning item: " + prefab.name);
        PhotonNetwork.Instantiate(prefab.name, this.transform.position, Quaternion.identity, 0);
    }
}

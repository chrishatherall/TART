using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;

public class UI_alert_manager : MonoBehaviour
{
    // Alert message prefab. Spawned when an alert is made
    public GameObject AlertPrefab;

    public float verticalSpacing = 50f;

    // Start is called before the first frame update
    void Start()
    {
        // Listen for game-level alerts
        if (!gm)
        {
            Debug.LogError("[UI_alert_manager] Could not find GM");
            this.enabled = false;
            return;
        }
        gm.OnGameAlert += AddAlert;
    }

    public void AddAlert(string msg)
    {
        if (!AlertPrefab)
        {
            gm.LogError("[UI_alert_manager] Missing AlertPrefab!");
            return;
        }

        // Get all AlertPrefabs
        UI_alert[] alerts = GetComponentsInChildren<UI_alert>();
        // Loop through items in list and shift them down (or tell them to shift)
        foreach (UI_alert a in alerts)
        {
            a.transform.Translate(new Vector3(0f, verticalSpacing));
        }
        // Spawn an alert prefab on our gameobject
        GameObject alert = Instantiate(AlertPrefab,this.transform);
        // Set details
        alert.GetComponent<UI_alert>().SetMessage(msg);

        gm.Log("[UI_alert] Added alert: " + msg);
    }
}

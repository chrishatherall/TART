using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class IntermittentLight : MonoBehaviour
{
    new Light light;

    [SerializeField]
    float offTime;
    [SerializeField]
    float onTime;

    float sTimeUntilChange;

    // Start is called before the first frame update
    void Start()
    {
        light = GetComponent<Light>();

        sTimeUntilChange = light.enabled ? onTime : offTime;
    }

    // Update is called once per frame
    void Update()
    {
        sTimeUntilChange -= Time.deltaTime;

        if (sTimeUntilChange < 0f)
        {
            light.enabled = !light.enabled;
            sTimeUntilChange = light.enabled ? onTime : offTime;
        }
    }
}

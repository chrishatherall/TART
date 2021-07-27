using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_commandWindow : MonoBehaviour
{
    // The command window label we should enable/disable on `
    [SerializeField]
    GameObject commandWindowObject;
    // The textbox where commands are entered.
    [SerializeField]
    InputField commandInput;
    // The text field inside the scrollable log window
    [SerializeField]
    Text logText;

    public void CommandEntered ()
    {
        
    }

    public void HandleLog(string message, bool isError)
    {
        logText.text += "\n" + message;
        // TODO colour errors in red
        // TODO scroll down automatically
    }

    public void HandleUnityLog(string logString, string stack, LogType type)
    {
        logText.text += "\n[UNITY] [" + type.ToString() + "] " + logString;
        if (type == LogType.Exception)
        {
            logText.text += "\n" + stack;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!commandWindowObject || !commandInput || !logText)
        {
            Debug.LogError("UI_commandWindow is missing references");
            this.enabled = false;
            return;
        }

        // Add hook to logs on the GameManager
        GameManager.gm.OnLog += HandleLog;
        // Add hook for unity logs
        Application.logMessageReceived += HandleUnityLog;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.BackQuote)) commandWindowObject.SetActive(!commandWindowObject.activeSelf);
    }
}

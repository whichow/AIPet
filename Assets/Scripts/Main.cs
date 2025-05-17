using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Vonweller;

public class Main : MonoBehaviour
{
    private string otaUrl = "https://api.tenclass.net/xiaozhi/ota/";
    private string websocketUrl = "wss://api.tenclass.net/xiaozhi/v1/";

    private VoiceAssistantClientSystem voiceAssistantClientSystem;

    // Start is called before the first frame update
    async void Start()
    {
        voiceAssistantClientSystem = new VoiceAssistantClientSystem();
        voiceAssistantClientSystem.Init();
        await voiceAssistantClientSystem.webSocket_IOT_Mqtt_Microphone_InIt(websocketUrl, otaUrl);
        Debug.Log($"Connecting: {websocketUrl} / {otaUrl}");

    }

    // Update is called once per frame
    async void Update()
    {
        await voiceAssistantClientSystem.Update();
    }
}

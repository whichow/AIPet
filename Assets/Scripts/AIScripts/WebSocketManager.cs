using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System;
using UnityEngine;
using System.Threading.Tasks;

public class WebSocketManager
{
    private ClientWebSocket ws;
    private CancellationTokenSource cancellationTokenSource;
    private Action<string> messageHandler { get; set; }
    public AudioManager audioManager;

    public async Task ConnectAsync(string url, string accessToken, string deviceMac, string deviceUuid, Action<string> onMessage)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
        }
        ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
        ws.Options.SetRequestHeader("Protocol-Version", "1");
        ws.Options.SetRequestHeader("Device-Id", deviceMac);
        ws.Options.SetRequestHeader("Client-Id", deviceUuid);
        cancellationTokenSource = new CancellationTokenSource();
        try
        {
            messageHandler = onMessage;
            await ws.ConnectAsync(new Uri(url), cancellationTokenSource.Token);
            Debug.Log("WebSocket connected");
            ReceiveMessagesAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket connection error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            Debug.Log("WebSocket disconnected");
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
             //Debug.LogWarning($"��������: {message}");
        }
    }

    public async Task SendBinaryAsync(byte[] data)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            //Debug.LogWarning($"��������:Binary");
        }
    }


    private async void ReceiveMessagesAsync()
    {
        Debug.Log("��ʼ��������");
        byte[] buffer = new byte[1024 * 4];
        while (ws.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.Log($"����::{message}");
                    messageHandler?.Invoke(message);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    //Debug.Log($"���յ���Ϣ MessageType: {result.MessageType}, ���ݳ���: {result.Count}");
                    byte[] opusData = new byte[result.Count];
                    Array.Copy(buffer, opusData, result.Count);

                    // ����Խ��յ��� Opus ���ݽ��н��룬ת��Ϊ PCM ����
                    float[] pcmData = audioManager.DecodeAudio(opusData);

                    if (pcmData != null && pcmData.Length > 0)
                    {
                        //Debug.Log($"����ɹ������ݳ��ȣ�{pcmData.Length}");
                        // ȷ�������Ƿ�����˷磬�����Բ�����Ƶ
                        audioManager.PlayAudio(pcmData);
                        // ������־��ȷ�ϲ��ų���
                        //Debug.Log("���Բ�����Ƶ����");
                    }
                    else
                    {
                        Debug.LogError("������ PCM ����Ϊ�ջ���Ч��");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                    break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket receive error: {ex.Message}");
            }
        }
    }

}

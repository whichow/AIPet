// using System;
// using System.Collections;
// using System.Collections.Generic;
// using OfficeOpenXml.FormulaParsing.Excel.Functions.RefAndLookup;
// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
// using Vonweller;
// using YukiFrameWork;

// public class Getcmaer : MonoBehaviour
// {
//     public Canvas canvas1;
//     public Canvas canvas2;
//     public RawImage UICamera;
//     public Text text;
//     public int vh = 0;

//     public TMP_Dropdown inputtextDropdown;

//     public TMP_Dropdown OTATextDropdown;

//     public Button 链接Button;

//     public Button 绿色主菜单Button;


//     public Button 微信模式Button;
//     public Button 打断重新链接Button;

//     private string OTA;
//     public GameObject AIchat;
//     public Text AIchattext;
//     public GameObject playerchar;
//     public Text playerchartext;
//     public RectTransform Content;
//     public ScrollRect scrollRect;
//     public GameObject WXLT;
//     private string URL;

//     public string[] OTA选择服务器 = new[]
//     {
//         "https://api.tenclass.net/xiaozhi/ota/",
//         "http://159.75.202.173:8002/xiaozhi/ota/"
//     };
//     public string[] IP选择服务器 = new[]
//     {
//         "wss://api.tenclass.net/xiaozhi/v1/",
//         "ws://159.75.202.173:8000/xiaozhi/v1/"
//     };
//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
//         OTA = OTA选择服务器[0];
//         URL = IP选择服务器[0];
//         var gb = GameObject.Find("HotGameStart(Clone)").GetComponent<HotGameStart>();
//         绿色主菜单Button.onClick.AddListener(() =>
//         {
//             微信模式Button.gameObject.SetActive(!微信模式Button.gameObject.activeSelf);
//             打断重新链接Button.gameObject.SetActive(!打断重新链接Button.gameObject.activeSelf);
//         });
//         链接Button.onClick.AddListener(() =>
//         {
//             链接Button.gameObject.SetActive(false);
//             Debug.Log("lj" + 链接Button.gameObject.activeSelf);
//             绿色主菜单Button.gameObject.SetActive(true);
//             if (string.IsNullOrEmpty(URL))
//             {
//                 URL = inputtextDropdown.options[0].text;
//             }
//             if (string.IsNullOrEmpty(OTA))
//             {
//                 OTA = OTATextDropdown.options[0].text;
//             }
//             GameObject.Find("HotGameStart(Clone)").
//                 GetComponent<HotGameStart>().
//                 GetSystem<VoiceAssistantClientSystem>().
//                 webSocket_IOT_Mqtt_Microphone_InIt(URL, OTA);
//             Debug.Log($"正在链接：{URL} == {OTA}");
//             inputtextDropdown.gameObject.SetActive(false);
//             OTATextDropdown.gameObject.SetActive(false);

//         });
//         inputtextDropdown.onValueChanged.AddListener((index) =>
//         {
//             Debug.Log("用户选择了选项: " + index);
//             Debug.Log("选项内容为: " + inputtextDropdown.options[index].text);
//             URL = IP选择服务器[index];
//         });
//         OTATextDropdown.onValueChanged.AddListener((index) =>
//         {
//             Debug.Log("用户选择了选项: " + index);
//             Debug.Log("选项内容为: " + OTATextDropdown.options[index].text);
//             OTA = OTA选择服务器[index];
//         });
//         微信模式Button.onClick.AddListener(() =>
//         {
//             Debug.Log("微信聊天模型开启");
//             WXLT.SetActive(!WXLT.activeSelf);
//             打断重新链接Button.gameObject.SetActive(false);
//             微信模式Button.gameObject.SetActive(false);
//         });
//         打断重新链接Button.onClick.AddListener(() =>
//         {
//             gb.GetSystem<VoiceAssistantClientSystem>().OnSpaceKeyPress();
//             打断重新链接Button.gameObject.SetActive(false);
//             微信模式Button.gameObject.SetActive(false);
//         });
//         //设置画布的相机
//         canvas1.worldCamera = GameObject.Find("UICamera").GetComponent<Camera>();
//         canvas2.worldCamera = GameObject.Find("UICamera").GetComponent<Camera>();

//     }


//     public void AddAIchat(string msg)
//     {
//         AIchattext.text = msg;
//         AIchat.Instantiate(Content.transform).Show();
//         LayoutRebuilder.ForceRebuildLayoutImmediate(Content.parent.GetComponent<RectTransform>());
//         StartCoroutine(ScrollToBottom());
//     }
//     public void Addplayerchat(string msg)
//     {
//         playerchartext.text = msg; 
//         playerchar.Instantiate(Content.transform).Show();
//         //强制刷新布局
//         LayoutRebuilder.ForceRebuildLayoutImmediate(Content.parent.GetComponent<RectTransform>());
//         StartCoroutine(ScrollToBottom());
//     }

//     private IEnumerator ScrollToBottom()
//     {
//         // 等待一帧，确保布局更新完成
//         yield return null;
//         scrollRect.normalizedPosition = new Vector2(0, 0);
//     }

//     public void settext(string msg) 
//     {
//         if (vh > 10)
//         {
//             vh = 0;
//             text.text = "";
//         }
//         text.text = msg+"\n";
//         vh++;
//     }

//     // Update is called once per frame

// }

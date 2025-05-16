// using System;
// using UnityEngine;
// using UnityEngine.Android;

// public class Test : MonoBehaviour
// {
//     // Start is called once before the first execution of Update after the MonoBehaviour is created

//     public AudioSource Audio;
//     public AudioClip recordingClip;
//     async void Start()
//     {
//         // ������˷�Ȩ��
//         RequestMicrophonePermission().Forget();
//         // ��ʼ¼��
//         StartRecording();
//         await Awaitable.WaitForSecondsAsync(5);
//         StopRecording();

//     }

//     public void StartRecording()
//     {
//         if (Microphone.devices.Length == 0)
//         {
//             Debug.LogWarning("δ��⵽��˷��豸���޷�¼��");
//             return;
//         }

//         try
//         {
//             recordingClip = Microphone.Start(null, true, 10, 16000);
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"¼��ʧ��: {e.Message}");
//         }
//     }

//     public void StopRecording()
//     {
//         Microphone.End(null);
//         Debug.Log("ֹͣ¼��");
//         Audio.clip = recordingClip;
//         Audio.Play();
//     }

//     private async YieldTask RequestMicrophonePermission()
//     {
//         Debug.Log($"������˷�Ȩ�ޣ���ǰƽ̨: {Application.platform}");

// #if PLATFORM_ANDROID
//         // Androidƽ̨���뱣�ֲ���
//         if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
//         {
//             Permission.RequestUserPermission(Permission.Microphone);

//             // �ȴ��û���ӦȨ������
//             float timeWaited = 0;
//             while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && timeWaited < 5.0f)
//             {
//                 await Awaitable.WaitForSecondsAsync(0.1f);
//                 timeWaited += 0.1f;
//             }

//             if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
//             {
//                 Debug.Log("��˷�Ȩ���ѻ�ȡ");
//             }
//             else
//             {
//                 Debug.LogWarning("�û��ܾ�����˷�Ȩ��");
//             }
//         }
//         else
//         {
//             Debug.Log("������˷�Ȩ��");
//         }
// #elif PLATFORM_IOS
//             // iOS ƽ̨�ϣ�������Ҫ�����˷�Ȩ��
//             // ע�⣺iOS ��Ҫ�� Info.plist ������ NSMicrophoneUsageDescription
            
//             // �� iOS �ϣ����ǿ���ͨ���������� Microphone ������Ȩ������
//             bool hasPermission = false;
            
//             // ����Ƿ��Ѿ�����˷��豸
//             if (Microphone.devices.Length > 0)
//             {
//                 try
//                 {
//                     // ���Կ�ʼ¼������ᴥ��Ȩ������
//                     string deviceName = Microphone.devices[0];
//                     int minFreq, maxFreq;
//                     Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
                    
//                     // ����һ�����ݵ�¼��������Ȩ������
//                     AudioClip tempClip = Microphone.Start(deviceName, false, 1, 16000);
                    
//                     // �ȴ�һС��ʱ�䣬ȷ��Ȩ�޶Ի�����ʾ������
//                     float timeWaited = 0;
//                     while (Microphone.IsRecording(deviceName) && timeWaited < 1.0f)
//                     {
//                         await Awaitable.WaitForSecondsAsync(0.1f);
//                         timeWaited += 0.1f;
//                     }
                    
//                     // ֹͣ��ʱ¼��
//                     Microphone.End(deviceName);
//                     UnityEngine.Object.Destroy(tempClip);
                    
//                     // �ٴμ���Ƿ�����˷��豸������У�����ΪȨ���ѻ�ȡ
//                     hasPermission = Microphone.devices.Length > 0;
                    
//                     if (hasPermission)
//                     {
//                         Debug.Log("iOS ��˷�Ȩ���ѻ�ȡ");
//                     }
//                     else
//                     {
//                         Debug.LogWarning("iOS �û����ܾܾ�����˷�Ȩ��");
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     Debug.LogError($"iOS ��˷�Ȩ������ʧ��: {ex.Message}");
//                 }
//             }
//             else
//             {
//                 Debug.LogWarning("iOS �豸��δ��⵽��˷�");
//             }
// #elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
//             // PCƽ̨��Windows��macOS��Linux���Ĵ���
//             Debug.Log("PCƽ̨�����˷�");
            
//             if (Microphone.devices.Length > 0)
//             {
//                 Debug.Log($"��⵽ {Microphone.devices.Length} ����˷��豸:");
//                 for (int i = 0; i < Microphone.devices.Length; i++)
//                 {
//                     Debug.Log($"  {i+1}. {Microphone.devices[i]}");
//                 }
                
//                 // ��PCƽ̨�ϣ����ǿ��Գ��Ի�ȡ��˷��豸������
//                 string deviceName = Microphone.devices[0];
//                 int minFreq, maxFreq;
//                 Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
                
//                 Debug.Log($"��˷� '{deviceName}' ֧�ֵ�Ƶ�ʷ�Χ: {minFreq}Hz - {maxFreq}Hz");
//                 Debug.Log("PCƽ̨��˷�Ȩ�޼�����");
//             }
//             else
//             {
//                 Debug.LogWarning("δ��⵽��˷��豸��¼�����ܽ�������");
//             }
// #else
//             // ����ƽ̨�ϣ����Ǽ����Ѿ���Ȩ��
//             Debug.Log("����ƽ̨������������˷�Ȩ��");
// #endif
//     }

// }

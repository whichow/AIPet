public class VoiceAssistantConfig
{
    public string accessToken = "test-token";
    public string deviceMac = "04:68:74:27:12:99";
    public string deviceUuid = "test-uuid";

    public string wsUrl = "wss://api.tenclass.net/xiaozhi/v1/";
    public string otaUrl = "https://api.tenclass.net/xiaozhi/ota/";

    // public string wsUrl = "ws://159.75.202.173:8000/xiaozhi/v1/";
    // public string otaUrl = "http://159.75.202.173:8002/xiaozhi/ota/";

    public bool manualMode = false;

    // VAD�������
    public bool useVAD = true;
    public float vadThreshold = 0.02f;  // Ĭ����ֵ��������Ҫ����ʵ�ʻ�������
    public int vadSilenceFrames = 30;   // Լ0.5��ľ����ж�Ϊ˵������
    public float ttsCooldownTime = 1.0f; // TTS���������ȴʱ��(��)
}
public static class Constants
{

    public static class  VLconfig
    {
        public static  string apiKey = "sk-5ae5e5b4e853487aac092031aa70de38"; // �滻Ϊ���API Key
        public static string voice = "Cherry";
    }
    // MQTT ����
    public static class Mqtt
    {
        public const string BrokerAddress = "iot.dfrobot.com.cn";
        public const int BrokerPort = 1883;
        public const string ClientId = "XiaozhiAI_Client";
        public const string Username = "W7xR5OmHg";
        public const string Password = "ZnxR5OiHRz";
        public const string TopicSubscribe = "beUljnoHgza";
        public const string TopicPublish = "beUljnoHg";
    }
}
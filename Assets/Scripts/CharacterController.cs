using UnityEngine;
using System.Collections.Generic;
using Vonweller;
using System.Collections;

public class CharacterController : MonoBehaviour
{
    public Animator animator;

    // 表情到动画状态名的映射
    private static readonly Dictionary<string, string> emotionToState = new Dictionary<string, string>
    {
        { "neutral", "default@unitychan" },
        { "happy", "smile1@unitychan" },
        { "laughing", "smile2@unitychan" },
        { "funny", "smile2@unitychan" },
        { "sad", "sap@unitychan" },
        { "angry", "angry1@unitychan" },
        { "crying", "sap@unitychan" },
        { "loving", "smile1@unitychan" },
        { "embarrassed", "ASHAMED" },
        { "surprised", "SURPRISE" },
        { "shocked", "SURPRISE" },
        { "thinking", "conf@unitychan" },
        { "winking", "smile1@unitychan" },
        { "cool", "default@unitychan" },
        { "relaxed", "default@unitychan" },
        { "delicious", "smile1@unitychan" },
        { "kissy", "smile1@unitychan" },
        { "confident", "conf@unitychan" },
        { "sleepy", "eye_close@unitychan" },
        { "silly", "smile2@unitychan" },
        { "confused", "conf@unitychan" }
    };

    private Coroutine emotionCoroutine;

    void Start()
    {
        var va = VoiceAssistantClientSystem.Instance;
        if (va != null)
        {
            va.OnEmotionReceived += OnEmotionReceived;
            va.OnAITextReceived += OnAITextReceived;
        }
        else
        {
            Debug.LogError("VoiceAssistantClientSystem.Instance is null!");
        }
    }

    void OnDestroy()
    {
        var va = VoiceAssistantClientSystem.Instance;
        if (va != null)
        {
            va.OnEmotionReceived -= OnEmotionReceived;
            va.OnAITextReceived -= OnAITextReceived;
        }
    }

    private void OnEmotionReceived(string emotion)
    {
        Debug.Log($"Received emotion: {emotion}");
        animator.SetLayerWeight(1, 1f);
        string stateName = EmotionToStateName(emotion);
        if (!string.IsNullOrEmpty(stateName))
        {
            animator.CrossFade(stateName, 0);
            if (emotionCoroutine != null)
                StopCoroutine(emotionCoroutine);
            emotionCoroutine = StartCoroutine(ResetToDefaultAfterDelay(stateName, 1.5f)); // 1.5秒后切回默认，可根据动画长度调整
        }
    }

    private void OnAITextReceived(string text)
    {
        // animator.CrossFade("AISpeak", 0.1f); // 你可以自定义AISpeak动画名
    }

    private string EmotionToStateName(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return "default@unitychan";
        emotion = emotion.ToLower().Trim();
        if (emotionToState.TryGetValue(emotion, out var state))
            return state;
        return "default@unitychan";
    }

    private IEnumerator ResetToDefaultAfterDelay(string stateName, float delay)
    {
        yield return new WaitForSeconds(delay);
        animator.CrossFade("default@unitychan", 0);
    }
}
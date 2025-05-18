using UnityEngine;
using System.Collections.Generic;
using Vonweller;
using System.Collections;

public class CharacterController : MonoBehaviour
{
    public Animator animator;

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

    private static readonly Dictionary<string, string> emotionToAction = new Dictionary<string, string>
    {
        { "neutral", "WAIT00" },
        { "happy", "WAIT01" },
        { "laughing", "WAIT02" },
        { "funny", "WAIT02" },
        { "sad", "WAIT03" },
        { "angry", "WAIT04" },
        { "crying", "WAIT03" },
        { "loving", "WAIT01" },
        { "embarrassed", "WAIT03" },
        { "surprised", "WAIT04" },
        { "shocked", "WAIT04" },
        { "thinking", "WAIT00" },
        { "winking", "WAIT01" },
        { "cool", "WAIT00" },
        { "relaxed", "WAIT00" },
        { "delicious", "WAIT01" },
        { "kissy", "WAIT01" },
        { "confident", "WAIT00" },
        { "sleepy", "WAIT03" },
        { "silly", "WAIT02" },
        { "confused", "WAIT03" }
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
        string actionName = EmotionToActionName(emotion);

        if (!string.IsNullOrEmpty(actionName))
            animator.CrossFade(actionName, 0.1f, 0);
        if (!string.IsNullOrEmpty(stateName))
            animator.CrossFade(stateName, 0.1f, 1);

        if (emotionCoroutine != null)
            StopCoroutine(emotionCoroutine);
        emotionCoroutine = StartCoroutine(ResetToDefaultAfterDelay(1.5f));
    }

    private void OnAITextReceived(string text)
    {
    }

    private string EmotionToStateName(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return "default@unitychan";
        emotion = emotion.ToLower().Trim();
        if (emotionToState.TryGetValue(emotion, out var state))
            return state;
        return "default@unitychan";
    }

    private string EmotionToActionName(string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return "WAIT00";
        emotion = emotion.ToLower().Trim();
        if (emotionToAction.TryGetValue(emotion, out var action))
            return action;
        return "WAIT00";
    }

    private IEnumerator ResetToDefaultAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        animator.CrossFade("WAIT00", 0.1f, 0);
        animator.CrossFade("default@unitychan", 0.1f, 1);
        animator.SetLayerWeight(1, 0f);
    }
}
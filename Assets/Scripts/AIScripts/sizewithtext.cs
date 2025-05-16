using UnityEngine;
using UnityEngine.UI;

public class sizewithtext : MonoBehaviour
{
    public Text text;

    public RectTransform self;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        self = this.gameObject.GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        self.sizeDelta = new Vector2(self.sizeDelta.x,
            text.preferredHeight);
    }
}

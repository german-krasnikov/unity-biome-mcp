using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    private RectTransform _mainPanel;
    private RectTransform _settingsPanel;
    private float _slide; // probe-trigger
    private const float Duration = 0.35f;

    void Awake()
    {
        _mainPanel = transform.Find("CenterGroup") as RectTransform;
        _settingsPanel = transform.Find("SettingsPanel") as RectTransform;
        _slide = GetComponent<CanvasScaler>()?.referenceResolution.x ?? 1920f;

        // Hide settings off-screen immediately
        SetX(_settingsPanel, _slide);

        // Wire buttons
        Bind("CenterGroup/SettingsBtn", OpenSettings);
        Bind("SettingsPanel/BackBtn", CloseSettings);
        Bind("CenterGroup/ExitBtn", () => Application.Quit());
    }

    void Bind(string path, UnityEngine.Events.UnityAction action)
    {
        transform.Find(path)?.GetComponent<Button>()?.onClick.AddListener(action);
    }

    void OpenSettings()
    {
        StopAllCoroutines();
        StartCoroutine(Slide(_mainPanel, 0, -_slide));
        StartCoroutine(Slide(_settingsPanel, _slide, 0));
    }

    void CloseSettings()
    {
        StopAllCoroutines();
        StartCoroutine(Slide(_mainPanel, -_slide, 0));
        StartCoroutine(Slide(_settingsPanel, 0, _slide));
    }

    static void SetX(RectTransform rt, float x)
    {
        var p = rt.anchoredPosition;
        p.x = x;
        rt.anchoredPosition = p;
    }

    static IEnumerator Slide(RectTransform rt, float from, float to)
    {
        float t = 0;
        while (t < Duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0, 1, t / Duration);
            SetX(rt, Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetX(rt, to);
    }
}

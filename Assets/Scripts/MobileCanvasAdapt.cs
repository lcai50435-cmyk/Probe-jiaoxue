using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public interface IMobileLayoutRefresh
{
    void RefreshMobileLayout();
}

/// <summary>把 1920x1080 教学内容完整居中显示，额外屏幕空间不参与业务坐标计算。</summary>
[DefaultExecutionOrder(-1000)]
public sealed class MobileCanvasAdapt : MonoBehaviour
{
    private static readonly Vector2 ReferenceSize = new Vector2(1920f, 1080f);
    private static readonly Color PageColor = Color.white;
    private const string PageBackgroundName = "~MobilePageBackground";
    private readonly Dictionary<RectTransform, AnchorState> _anchors = new Dictionary<RectTransform, AnchorState>();
    private int _width;
    private int _height;

    private struct AnchorState
    {
        public Vector2 min;
        public Vector2 max;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<MobileCanvasAdapt>() != null) return;
        var host = new GameObject("~MobileCanvasAdapt") { hideFlags = HideFlags.DontSave };
        DontDestroyOnLoad(host);
        host.AddComponent<MobileCanvasAdapt>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyForCurrentScreen();
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Update()
    {
        if (_width != Screen.width || _height != Screen.height) ApplyForCurrentScreen();
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        _anchors.Clear();
        ApplyForCurrentScreen();
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        ApplyForCurrentScreen();
    }

    private void ApplyForCurrentScreen()
    {
        _width = Screen.width;
        _height = Screen.height;
        if (_width <= 0 || _height <= 0) return;

        var wide = _width / (float)_height >= ReferenceSize.x / ReferenceSize.y;
        var scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        foreach (var scaler in scalers)
        {
            if (!IsTarget(scaler)) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = wide ? 1f : 0f;
        }

        Canvas.ForceUpdateCanvases();
        foreach (var scaler in scalers)
        {
            if (!IsTarget(scaler)) continue;
            var canvasRt = scaler.transform as RectTransform;
            EnsurePageBackground(canvasRt);
            FitDirectChildren(canvasRt);
        }
        Canvas.ForceUpdateCanvases();
        foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            if (camera != null && camera.gameObject.scene.isLoaded) camera.backgroundColor = PageColor;
        RefreshLayoutGeometry();
    }

    private static bool IsTarget(CanvasScaler scaler)
    {
        return scaler != null && scaler.gameObject.scene.isLoaded &&
               (scaler.transform.parent == null || scaler.transform.parent.GetComponentInParent<Canvas>() == null) &&
               scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
               scaler.referenceResolution == ReferenceSize;
    }

    private static void EnsurePageBackground(RectTransform canvasRt)
    {
        if (canvasRt == null) return;
        foreach (Transform child in canvasRt)
            if (child.name == PageBackgroundName && child.gameObject.hideFlags == HideFlags.DontSave) return;

        var background = new GameObject(PageBackgroundName, typeof(RectTransform), typeof(Image))
        {
            hideFlags = HideFlags.DontSave
        };
        var rt = background.GetComponent<RectTransform>();
        rt.SetParent(canvasRt, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var image = background.GetComponent<Image>();
        image.color = PageColor;
        image.raycastTarget = false;
        rt.SetAsFirstSibling();
    }

    private void FitDirectChildren(RectTransform canvasRt)
    {
        if (canvasRt == null || canvasRt.rect.width <= 0f || canvasRt.rect.height <= 0f) return;
        var parentSize = canvasRt.rect.size;
        foreach (Transform child in canvasRt)
        {
            if (!(child is RectTransform rt)) continue;
            if (rt.name == PageBackgroundName && rt.gameObject.hideFlags == HideFlags.DontSave) continue;
            if (!_anchors.TryGetValue(rt, out var state))
            {
                state = new AnchorState { min = rt.anchorMin, max = rt.anchorMax };
                _anchors.Add(rt, state);
            }
            rt.anchorMin = MapAnchor(state.min, parentSize);
            rt.anchorMax = MapAnchor(state.max, parentSize);
        }
    }

    private static Vector2 MapAnchor(Vector2 anchor, Vector2 parentSize)
    {
        return new Vector2(
            .5f + (anchor.x - .5f) * ReferenceSize.x / parentSize.x,
            .5f + (anchor.y - .5f) * ReferenceSize.y / parentSize.y);
    }

    private static void RefreshLayoutGeometry()
    {
        foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (behaviour != null && behaviour.gameObject.scene.isLoaded && behaviour is IMobileLayoutRefresh target)
                target.RefreshMobileLayout();
        }
    }
}

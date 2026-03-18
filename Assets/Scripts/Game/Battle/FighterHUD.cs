using System.Collections;
using UnityEngine;
using TMPro;

public class FighterHUD : MonoBehaviour
{
    public float VerticalOffset = 1.6f;
    public float BarWidth = 1.2f;
    public float BarHeight = 0.12f;

    private GameObject _bgQuad;
    private GameObject _fgQuad;
    private int _maxHp = 1;
    private int _currentHp = 1;

    private void Awake()
    {
        CreateBar();
    }

    private void LateUpdate()
    {
        // Make HUD quads face camera
        if (Camera.main != null)
        {
            Quaternion camRot = Camera.main.transform.rotation;
            if (_bgQuad != null) _bgQuad.transform.rotation = camRot;
            if (_fgQuad != null) _fgQuad.transform.rotation = camRot;
        }
    }

    private void CreateBar()
    {
        // Background quad
        _bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _bgQuad.name = "HPBarBg";
        _bgQuad.transform.SetParent(transform, false);
        _bgQuad.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);
        _bgQuad.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
        var bgRenderer = _bgQuad.GetComponent<MeshRenderer>();
        bgRenderer.material = new Material(Shader.Find("Unlit/Color"));
        bgRenderer.material.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        // remove collider
        var bgCol = _bgQuad.GetComponent<Collider>(); if (bgCol != null) Object.Destroy(bgCol);

        // Foreground quad
        _fgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fgQuad.name = "HPBarFg";
        _fgQuad.transform.SetParent(transform, false);
        _fgQuad.transform.localPosition = new Vector3(- (BarWidth - BarWidth) / 2f, VerticalOffset, -0.01f);
        _fgQuad.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
        var fgRenderer = _fgQuad.GetComponent<MeshRenderer>();
        fgRenderer.material = new Material(Shader.Find("Unlit/Color"));
        fgRenderer.material.color = new Color(0.18f, 0.8f, 0.22f, 0.95f);
        var fgCol = _fgQuad.GetComponent<Collider>(); if (fgCol != null) Object.Destroy(fgCol);
    }

    public void Initialize(int maxHp)
    {
        _maxHp = Mathf.Max(1, maxHp);
        _currentHp = _maxHp;
        UpdateBar();
    }

    public void UpdateHp(int newHp)
    {
        _currentHp = Mathf.Clamp(newHp, 0, _maxHp);
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (_fgQuad == null || _bgQuad == null) return;
        float t = _maxHp > 0 ? (float)_currentHp / _maxHp : 0f;
        t = Mathf.Clamp01(t);
        // Adjust foreground scale and position so left aligns with bg
        _fgQuad.transform.localScale = new Vector3(BarWidth * t, BarHeight, 1f);
        float half = BarWidth * 0.5f;
        _fgQuad.transform.localPosition = new Vector3(-half + (BarWidth * t) * 0.5f, VerticalOffset, -0.01f);
    }

    public void ShowDamage(int amount)
    {
        StartCoroutine(DoShowDamage(amount));
    }

    private IEnumerator DoShowDamage(int amount)
    {
        GameObject txtGo = new GameObject("DamageText", typeof(TextMeshPro));
        txtGo.transform.SetParent(transform, false);
        txtGo.transform.localPosition = new Vector3(0f, VerticalOffset + 0.4f, -0.02f);
        var tmp = txtGo.GetComponent<TextMeshPro>();
        tmp.text = amount.ToString();
        tmp.fontSize = 3f;
        tmp.color = Color.red;
        tmp.alignment = TextAlignmentOptions.Center;

        float elapsed = 0f;
        float duration = 0.9f;
        Vector3 start = txtGo.transform.localPosition;
        Vector3 end = start + new Vector3(0f, 0.6f, 0f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / duration;
            txtGo.transform.localPosition = Vector3.Lerp(start, end, p);
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f - p);
            if (Camera.main != null) txtGo.transform.rotation = Camera.main.transform.rotation;
            yield return null;
        }

        Object.Destroy(txtGo);
    }
}

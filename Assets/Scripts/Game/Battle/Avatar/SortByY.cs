using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SortByY : MonoBehaviour
{
    [Tooltip("Base sorting order to offset from (keeps groups separated)")]
    public int BaseOrder = 0;

    [Tooltip("Multiplier applied to Y position when computing order. Higher gives finer granularity.")]
    public int Multiplier = 100;

    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (_renderer == null)
        {
            return;
        }

        // Smaller world Y (lower on screen) should appear in front -> higher sortingOrder
        int order = BaseOrder + Mathf.RoundToInt(-transform.position.y * Multiplier);
        if (_renderer.sortingOrder != order)
        {
            _renderer.sortingOrder = order;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 饰品背包面板 - 展示所有已收集和未收集的饰品
    /// </summary>
    public class BackpackPanel : MonoBehaviour
    {
        [Header("预制体引用")]
        [SerializeField] private GameObject _itemPrefab;

        [Header("UI 引用")]
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private GameObject _emptyHint;
        [SerializeField] private Button _closeButton;

        private readonly List<GameObject> _entries = new List<GameObject>();

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            ClearEntries();
            BuildAccessoryList();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ClearEntries()
        {
            foreach (var entry in _entries)
            {
                if (entry != null) Destroy(entry);
            }
            _entries.Clear();
        }

        private void BuildAccessoryList()
        {
            if (_contentRoot == null || _itemPrefab == null) return;

            var playerData = GameManager.Instance?.DataManager?.PlayerData;
            if (playerData == null) return;

            var equipments = playerData.runEquipments;
            if (equipments == null || equipments.Count == 0)
            {
                if (_emptyHint != null) _emptyHint.SetActive(true);
                return;
            }

            if (_emptyHint != null) _emptyHint.SetActive(false);

            foreach (var equip in equipments)
            {
                GameEffect primaryEffect = GameEffect.AttackPercent;
                if (equip.effects != null && equip.effects.Count > 0 && equip.effects[0].gameEffectType >= 0)
                {
                    primaryEffect = (GameEffect)equip.effects[0].gameEffectType;
                }

                string scopeLabel = equip.GetScopeDisplayString();
                string applyLabel = equip.GetApplyTypeDisplayString();
                string fullDesc = equip.description;
                if (!string.IsNullOrEmpty(scopeLabel))
                    fullDesc += $" [{scopeLabel}/{applyLabel}]";

                GameObject itemGo = Instantiate(_itemPrefab, _contentRoot);
                itemGo.name = $"Item_{equip.equipmentId}";
                BackpackItem item = itemGo.GetComponent<BackpackItem>();
                if (item != null)
                {
                    item.Setup(equip.displayName, fullDesc, primaryEffect);
                }
                _entries.Add(itemGo);
            }
        }
    }
}

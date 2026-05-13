using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;
using System.Collections.Generic;

namespace TribeSystem.UI
{
    /// <summary>
    /// 族群详细信息弹窗 - 点击 TribeCard 时显示
    /// </summary>
    public class TribeDetailTips : MonoBehaviour
    {
        [Header("UI 组件")]
        [SerializeField] private Text _tribeNameText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _leaderNameText;
        [SerializeField] private Text _leaderStatsText;
        [SerializeField] private RectTransform _catsListContainer;

        private TribeRecord _tribe;
        private bool _isDeployed;
        private Action<TribeRecord> _onRestClicked;
        private Action<TribeRecord, bool> _onDeployChanged;
        private Action _onClosed;

        // 小猫卡片预制体（Addressables 缓存）
        private GameObject _littleCatCardPrefab;
        private bool _prefabLoading;
        // ScrollRect 运行时构建后的 Content 节点
        private RectTransform _catsContentRect;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Hide);
            }
        }

        /// <summary>
        /// 显示族群详细信息
        /// </summary>
        public void Show(TribeRecord tribe, bool isDeployed, Action<TribeRecord> onRestClicked, Action<TribeRecord, bool> onDeployChanged, Action onClosed = null)
        {
            _tribe = tribe;
            _isDeployed = isDeployed;
            _onRestClicked = onRestClicked;
            _onDeployChanged = onDeployChanged;
            _onClosed = onClosed;

            UpdateContent();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _onClosed?.Invoke();
            _onClosed = null;
            gameObject.SetActive(false);
        }

        public void ClearOnClosed()
        {
            _onClosed = null;
        }

        private void UpdateContent()
        {
            if (_tribe == null) return;

            // 兵种名称（从 fighter 表获取）
            if (_tribeNameText != null)
            {
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(_tribe.fighterId);
                _tribeNameText.text = fighterConfig?.fighterName ?? $"兵种{_tribe.fighterId}";
            }

            // 首个单位信息
            if (_leaderNameText != null)
            {
                if (_tribe.units != null && _tribe.units.Count > 0)
                {
                    var firstUnit = _tribe.units[0];
                    var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(firstUnit.fighterId);
                    _leaderNameText.text = fighterConfig?.fighterName ?? $"兵种{firstUnit.fighterId}";
                }
                else
                {
                    _leaderNameText.text = "无单位";
                }
            }

            // 首个单位属性
            if (_leaderStatsText != null && _tribe.units != null && _tribe.units.Count > 0)
            {
                var unit = _tribe.units[0];
                _leaderStatsText.text = $"攻击: {Mathf.RoundToInt(unit.staticAttack)}\n" +
                                        $"防御: {Mathf.RoundToInt(unit.staticDefense)}\n" +
                                        $"血量: {Mathf.RoundToInt(unit.staticHp)}\n" +
                                        $"速度: {Mathf.RoundToInt(unit.staticMoveSpeed * 1000)}";
            }

            // 更新小猫列表
            UpdateCatsList();
        }

        private void UpdateCatsList()
        {
            if (_catsListContainer == null || _tribe?.units == null) return;

            EnsureScrollView();

            // 清空 Content 内现有卡片
            RectTransform content = _catsContentRect ?? _catsListContainer;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            if (_littleCatCardPrefab != null)
            {
                PopulateCatCards(content);
            }
            else if (!_prefabLoading)
            {
                _prefabLoading = true;
                var handle = Addressables.LoadAssetAsync<GameObject>("ui/tribebuild/littlecatcard");
                handle.Completed += op =>
                {
                    _prefabLoading = false;
                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        _littleCatCardPrefab = op.Result;
                        // 如果弹窗仍然可见，重新填充
                        if (gameObject.activeInHierarchy && _tribe != null)
                        {
                            RectTransform c = _catsContentRect ?? _catsListContainer;
                            for (int i = c.childCount - 1; i >= 0; i--)
                                Destroy(c.GetChild(i).gameObject);
                            PopulateCatCards(c);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[TribeDetailTips] Failed to load ui/tribebuild/littlecatcard");
                    }
                };
            }
        }

        /// <summary>
        /// 将 _catsListContainer 替换为内含 ScrollRect 的可滚动布局（只执行一次）
        /// </summary>
        private void EnsureScrollView()
        {
            if (_catsContentRect != null) return;

            // 在 _catsListContainer 内构建 ScrollRect + Viewport + Content
            var scrollGo = new GameObject("CatsScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_catsListContainer, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;

            // Viewport
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            vpGo.transform.SetParent(scrollRect, false);
            var vpRect = vpGo.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            vpGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 透明但保留 Mask
            vpGo.GetComponent<Mask>().showMaskGraphic = false;
            sr.viewport = vpRect;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(vpRect, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.content = contentRect;
            _catsContentRect = contentRect;
        }

        private void PopulateCatCards(RectTransform content)
        {
            foreach (var unit in _tribe.units)
            {
                var cardGo = Instantiate(_littleCatCardPrefab, content, false);
                var card = cardGo.GetComponent<LittleCatCard>();
                card.Setup(unit, _tribe);
            }
        }
    }
}

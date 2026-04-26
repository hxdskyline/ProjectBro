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

            // 族群名称
            if (_tribeNameText != null)
            {
                _tribeNameText.text = GetTribeTypeName(_tribe.tribeType) + "族";
            }

            // 族长信息
            if (_leaderNameText != null)
            {
                _leaderNameText.text = _tribe.leader?.name ?? "无族长";
            }

            // 族长属性
            if (_leaderStatsText != null && _tribe.leader != null)
            {
                var leader = _tribe.leader;
                _leaderStatsText.text = $"攻击: {leader.baseAttack}\n" +
                                        $"防御: {leader.baseDefense}\n" +
                                        $"血量: {leader.baseHp}\n" +
                                        $"速度: {leader.baseSpeed}\n" +
                                        $"统御: {leader.command}";
            }

            // 更新小猫列表
            UpdateCatsList();
        }

        private void UpdateCatsList()
        {
            if (_catsListContainer == null || _tribe?.cats == null) return;

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
            foreach (var cat in _tribe.cats)
            {
                var cardGo = Instantiate(_littleCatCardPrefab, content, false);
                var card = cardGo.GetComponent<LittleCatCard>();
                card.Setup(cat, _tribe);
            }
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                default: return type.ToString();
            }
        }
    }
}

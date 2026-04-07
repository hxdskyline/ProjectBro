using UnityEngine;
using UnityEngine.UI;

namespace TribeSystem.UI
{
    /// <summary>
    /// 饰品图鉴面板 - 占位面板，后续实现具体饰品数据
    /// </summary>
    public class AccessoryCodexPanel : MonoBehaviour
    {
        private RectTransform _externalRoot;
        private bool _isRuntimeCreated;

        /// <summary>
        /// 设置外部根节点
        /// </summary>
        public void SetExternalRoot(RectTransform externalRoot)
        {
            _externalRoot = externalRoot;
        }

        /// <summary>
        /// 初始化面板（运行时创建UI）
        /// </summary>
        public void Initialize()
        {
            EnsureRuntimeUI();
        }

        private void EnsureRuntimeUI()
        {
            if (_isRuntimeCreated) return;
            _isRuntimeCreated = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform targetParent = _externalRoot != null ? _externalRoot : transform as RectTransform;

            // 背景面板
            GameObject panelGo = new GameObject("CodexContent", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(targetParent, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.15f);
            panelRect.anchorMax = new Vector2(0.8f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);

            // 标题
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelRect, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 28;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "饰品图鉴";

            // 占位提示
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(panelRect, false);
            RectTransform hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.3f);
            hintRect.anchorMax = new Vector2(0.9f, 0.7f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            Text hintText = hintGo.GetComponent<Text>();
            hintText.font = font;
            hintText.fontSize = 20;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            hintText.text = "暂无饰品数据\n敬请期待";

            // 关闭按钮
            GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panelRect, false);
            RectTransform closeRect = closeGo.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.35f, 0.05f);
            closeRect.anchorMax = new Vector2(0.65f, 0.15f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            Button closeBtn = closeGo.GetComponent<Button>();
            Image closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.5f, 0.25f, 0.2f, 1f);
            closeBtn.targetGraphic = closeImg;

            GameObject closeLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
            closeLabel.transform.SetParent(closeRect, false);
            RectTransform clRect = closeLabel.GetComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            Text clText = closeLabel.GetComponent<Text>();
            clText.font = font;
            clText.fontSize = 18;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.color = Color.white;
            clText.text = "关闭";

            closeBtn.onClick.AddListener(Hide);
        }

        public void Show()
        {
            EnsureRuntimeUI();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TribeSystem.UI
{
    /// <summary>
    /// 招募结果展示面板
    /// 类型A：小猫列表对比（繁育/品质/新族群）
    /// 类型B：族长属性对比（属性提升）
    /// </summary>
    public class RecruitmentResultPanel : MonoBehaviour
    {
        private RectTransform _contentArea;
        private Text _titleText;
        private Button _confirmButton;
        private bool _uiBuilt;
        private AsyncOperationHandle<Sprite> _portraitHandleBefore;
        private AsyncOperationHandle<Sprite> _portraitHandleAfter;

        public void ShowCatListResult(string tribeName, List<CatData> beforeCats, List<CatData> afterCats, TribeRecord tribe, Action onConfirmed)
        {
            EnsureUI();
            ClearContent();
            _titleText.text = $"{tribeName}族 繁育结果";

            // 左右两栏
            var hlg = _contentArea.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = _contentArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 5, 5);

            BuildCatListColumn(_contentArea, "提升前", beforeCats, tribe, 400f);
            BuildCatListColumn(_contentArea, "提升后", afterCats, tribe, 400f);

            WireConfirmButton(onConfirmed);
            Show();
        }

        public void ShowLeaderBoostResult(string tribeName, LeaderData leader, int bonusAttack, int bonusHp, int fighterId, Action onConfirmed)
        {
            EnsureUI();
            ClearContent();
            _titleText.text = $"{tribeName}族 属性提升";

            var hlg = _contentArea.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = _contentArea.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 5, 5);

            // 旧值面板：减去本次加成
            BuildLeaderCardFlat(_contentArea, leader, fighterId,
                -(bonusAttack), -(bonusHp), false);

            // 箭头
            var arrowGo = CreateText("Arrow", _contentArea, "→", 36, Color.white);
            var arrowRect = arrowGo.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(40f, 300f);

            // 新值面板：当前值，标记提升属性
            BuildLeaderCardFlat(_contentArea, leader, fighterId,
                bonusAttack, bonusHp, true);

            WireConfirmButton(onConfirmed);
            Show();
        }

        #region Cat List Column

        private void BuildCatListColumn(RectTransform parent, string header, List<CatData> cats, TribeRecord tribe, float width)
        {
            // 列容器
            var colGo = new GameObject($"Column_{header}", typeof(RectTransform), typeof(Image));
            colGo.transform.SetParent(parent, false);
            var colRect = colGo.GetComponent<RectTransform>();
            colRect.sizeDelta = new Vector2(width, 320f);
            colGo.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.15f, 0.9f);

            // 纵向布局
            var colVlg = colGo.AddComponent<VerticalLayoutGroup>();
            colVlg.spacing = 4f;
            colVlg.padding = new RectOffset(8, 8, 8, 8);
            colVlg.childAlignment = TextAnchor.UpperCenter;
            colVlg.childControlWidth = true;
            colVlg.childControlHeight = true;
            colVlg.childForceExpandWidth = true;
            colVlg.childForceExpandHeight = true;

            // 标题
            var headerGo = CreateText("Header", colRect, header, 22, new Color(1f, 0.85f, 0.4f));
            var headerLe = headerGo.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 28f;
            headerLe.minHeight = 28f;

            // ScrollView
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(colRect, false);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            // 当由 LayoutGroup 控制时，不需手动设置 anchor/offset
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);

            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            scrollRect.viewport = viewportRt;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var contentVlg = contentGo.GetComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 3f;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperCenter;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            // 填充小猫 items
            if (cats != null)
            {
                foreach (var cat in cats)
                    BuildCatItem(contentRt, cat, tribe);
            }
        }

        private void BuildCatItem(RectTransform parent, CatData cat, TribeRecord tribe)
        {
            var itemGo = new GameObject("CatItem", typeof(RectTransform), typeof(Image));
            itemGo.transform.SetParent(parent, false);
            var itemRt = itemGo.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(0, 36f);

            var bgImg = itemGo.GetComponent<Image>();
            bgImg.color = GetQualityBgColor(cat.quality);

            var le = itemGo.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 品质名
            var qualityGo = new GameObject("Quality", typeof(RectTransform), typeof(Text));
            qualityGo.transform.SetParent(itemGo.transform, false);
            var qualityRt = qualityGo.GetComponent<RectTransform>();
            qualityRt.anchorMin = new Vector2(0, 0);
            qualityRt.anchorMax = new Vector2(0.3f, 1);
            qualityRt.offsetMin = new Vector2(6, 0);
            qualityRt.offsetMax = new Vector2(0, 0);
            var qualityText = qualityGo.GetComponent<Text>();
            qualityText.font = font;
            qualityText.fontSize = 16;
            qualityText.alignment = TextAnchor.MiddleLeft;
            qualityText.color = GetQualityTextColor(cat.quality);
            qualityText.text = GetQualityLabel(cat.quality);

            // 属性
            var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(Text));
            statsGo.transform.SetParent(itemGo.transform, false);
            var statsRt = statsGo.GetComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0.3f, 0);
            statsRt.anchorMax = new Vector2(1, 1);
            statsRt.offsetMin = new Vector2(4, 0);
            statsRt.offsetMax = new Vector2(-4, 0);
            var statsText = statsGo.GetComponent<Text>();
            statsText.font = font;
            statsText.fontSize = 14;
            statsText.alignment = TextAnchor.MiddleLeft;
            statsText.color = new Color(0.9f, 0.9f, 0.9f);

            if (tribe?.leader != null)
            {
                var l = tribe.leader;
                int atk = Mathf.RoundToInt(l.baseAttack * cat.attackMultiplier);
                int def = Mathf.RoundToInt(l.baseDefense * cat.defenseMultiplier);
                int hp = Mathf.RoundToInt(l.baseHp * cat.hpMultiplier);
                int spd = Mathf.RoundToInt(l.baseMoveSpeed * cat.speedMultiplier * 1000);
                statsText.text = $"攻{atk} 防{def} 血{hp} 速{spd}";
            }
            else
            {
                statsText.text = $"攻{cat.attackMultiplier:P0} 防{cat.defenseMultiplier:P0} 血{cat.hpMultiplier:P0} 速{cat.speedMultiplier:P0}";
            }
        }

        #endregion

        #region Leader Card

        private void BuildLeaderCard(RectTransform parent, LeaderData leader, int fighterId,
            StatType? boostedStat, float? buffOverrideForBoosted, bool highlight)
        {
            var cardGo = new GameObject("LeaderCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(280f, 320f);
            cardGo.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.15f, 0.9f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 4f;
            cardVlg.padding = new RectOffset(10, 10, 10, 10);
            cardVlg.childAlignment = TextAnchor.UpperCenter;
            cardVlg.childControlWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childForceExpandHeight = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 头像
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(cardRt, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.sizeDelta = new Vector2(80, 80);
            var portraitLe = portraitGo.AddComponent<LayoutElement>();
            portraitLe.preferredHeight = 80f;
            LoadPortrait(portraitGo.GetComponent<Image>(), fighterId, boostedStat.HasValue);

            // 兵种名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{fighterId}";
            var nameGo = CreateText("FighterName", cardRt, fighterName, 22, new Color(1f, 0.85f, 0.4f));
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 28f;

            // 4项属性
            string[] attrNames = { "攻击", "防御", "生命", "速度" };
            int[] baseValues = { leader.baseAttack, leader.baseDefense, leader.baseHp, Mathf.RoundToInt(leader.baseMoveSpeed * 1000) };
            float[] buffPcts = {
                leader.permanentBuffs?.attackPercent ?? 0f,
                leader.permanentBuffs?.defensePercent ?? 0f,
                leader.permanentBuffs?.hpPercent ?? 0f,
                leader.permanentBuffs?.speedPercent ?? 0f
            };

            for (int i = 0; i < 4; i++)
            {
                bool isBoosted = boostedStat.HasValue && (int)boostedStat.Value == i;
                float buffPct = buffPcts[i];
                // 对于提升的属性，使用 override 值（如 before 传 buff-0.2，after 传 buff）
                if (isBoosted && buffOverrideForBoosted.HasValue)
                    buffPct = buffOverrideForBoosted.Value;

                int finalVal = Mathf.RoundToInt(baseValues[i] * (1f + buffPct));
                bool showHighlight = highlight && isBoosted;
                Color textColor = showHighlight ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.984f, 0.965f, 0.855f); // green or #fbf6da
                string prefix = showHighlight ? "▲ " : "";
                string label = $"{prefix}{attrNames[i]}: {finalVal}";

                var attrGo = CreateText($"Attr_{attrNames[i]}", cardRt, label, 18, textColor);
                var attrLe = attrGo.AddComponent<LayoutElement>();
                attrLe.preferredHeight = 26f;
            }
        }

        private void BuildLeaderCardFlat(RectTransform parent, LeaderData leader, int fighterId,
            int deltaAttack, int deltaHp, bool highlight)
        {
            var cardGo = new GameObject("LeaderCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(280f, 320f);
            cardGo.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.15f, 0.9f);

            var cardVlg = cardGo.AddComponent<VerticalLayoutGroup>();
            cardVlg.spacing = 4f;
            cardVlg.padding = new RectOffset(10, 10, 10, 10);
            cardVlg.childAlignment = TextAnchor.UpperCenter;
            cardVlg.childControlWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childForceExpandHeight = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 头像
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(cardRt, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.sizeDelta = new Vector2(80, 80);
            var portraitLe = portraitGo.AddComponent<LayoutElement>();
            portraitLe.preferredHeight = 80f;
            LoadPortrait(portraitGo.GetComponent<Image>(), fighterId, highlight);

            // 兵种名称
            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(fighterId);
            string fighterName = fighterConfig?.fighterName ?? $"兵种{fighterId}";
            var nameGo = CreateText("FighterName", cardRt, fighterName, 22, new Color(1f, 0.85f, 0.4f));
            var nameLe = nameGo.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 28f;

            // 属性（攻击、防御、生命）
            var buffs = leader.permanentBuffs;
            int atk = leader.baseAttack + (buffs?.attackBonus ?? 0) + deltaAttack;
            int def = leader.baseDefense + (buffs?.defenseBonus ?? 0);
            int hp = leader.baseHp + (buffs?.hpBonus ?? 0) + deltaHp;

            bool showAtk = highlight ? deltaAttack > 0 : deltaAttack < 0;
            bool showHp = highlight ? deltaHp > 0 : deltaHp < 0;

            string[] names = { "攻击", "防御", "生命" };
            int[] vals = { atk, def, hp };
            bool[] boosted = { showAtk, false, showHp };

            for (int i = 0; i < 3; i++)
            {
                Color textColor = boosted[i] ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.984f, 0.965f, 0.855f);
                string prefix = boosted[i] ? "▲ " : "";
                var attrGo = CreateText($"Attr_{names[i]}", cardRt, $"{prefix}{names[i]}: {vals[i]}", 18, textColor);
                var attrLe = attrGo.AddComponent<LayoutElement>();
                attrLe.preferredHeight = 26f;
            }
        }

        private void LoadPortrait(Image portraitImage, int fighterId, bool isAfter)
        {
            string address = GetTribePortraitAddress(fighterId);
            if (string.IsNullOrEmpty(address)) return;

            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            handle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && portraitImage != null)
                    portraitImage.sprite = op.Result;
            };

            if (isAfter)
            {
                if (_portraitHandleAfter.IsValid()) Addressables.Release(_portraitHandleAfter);
                _portraitHandleAfter = handle;
            }
            else
            {
                if (_portraitHandleBefore.IsValid()) Addressables.Release(_portraitHandleBefore);
                _portraitHandleBefore = handle;
            }
        }

        private string GetTribePortraitAddress(int fighterId)
        {
            return TribeConfigLoader.Instance?.GetFighterAvatarAddress(fighterId, 1);
        }

        #endregion

        #region UI Skeleton

        private void EnsureUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            // 全屏遮罩
            var maskImg = gameObject.GetComponent<Image>();
            if (maskImg == null) maskImg = gameObject.AddComponent<Image>();
            maskImg.color = new Color(0, 0, 0, 0.6f);

            var maskBtn = gameObject.AddComponent<Button>();
            maskBtn.targetGraphic = maskImg;
            // 点遮罩不关闭，只通过确定按钮关闭

            // 面板
            var panelGo = new GameObject("PanelContent", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.15f);
            panelRect.anchorMax = new Vector2(0.85f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.1f, 0.98f);

            var panelVlg = panelGo.AddComponent<VerticalLayoutGroup>();
            panelVlg.spacing = 6f;
            panelVlg.padding = new RectOffset(10, 10, 10, 10);
            panelVlg.childAlignment = TextAnchor.UpperCenter;
            panelVlg.childControlWidth = true;
            panelVlg.childControlHeight = true;
            panelVlg.childForceExpandWidth = true;
            panelVlg.childForceExpandHeight = true;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 标题
            _titleText = CreateTextComponent("Title", panelRect, font, 26, Color.white, TextAnchor.MiddleCenter);
            var titleLe = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 36f;
            titleLe.minHeight = 36f;
            titleLe.flexibleHeight = 0f;

            // 内容区
            var contentGo = new GameObject("ContentArea", typeof(RectTransform));
            contentGo.transform.SetParent(panelRect, false);
            _contentArea = contentGo.GetComponent<RectTransform>();
            var contentLe = contentGo.AddComponent<LayoutElement>();
            contentLe.flexibleHeight = 1f;
            contentLe.minHeight = 200f;

            // 确定按钮
            var btnGo = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panelRect, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(160, 50);
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredHeight = 50f;
            btnLe.preferredWidth = 160f;
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.2f, 0.5f, 0.3f, 1f);
            _confirmButton = btnGo.GetComponent<Button>();
            _confirmButton.targetGraphic = btnImg;

            var labelGo = CreateText("Label", btnRt, "确定", 22, Color.white);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            gameObject.SetActive(false);
        }

        private void ClearContent()
        {
            if (_contentArea == null) return;
            for (int i = _contentArea.childCount - 1; i >= 0; i--)
                Destroy(_contentArea.GetChild(i).gameObject);

            // 移除旧的 HLG
            var oldHlg = _contentArea.GetComponent<HorizontalLayoutGroup>();
            if (oldHlg != null) Destroy(oldHlg);
        }

        private void WireConfirmButton(Action onConfirmed)
        {
            if (_confirmButton == null) return;
            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(() =>
            {
                Hide();
                onConfirmed?.Invoke();
            });
        }

        #endregion

        #region Show/Hide

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion

        #region Helpers

        private float GetBuffPct(PermanentBuffs buffs, StatType statType)
        {
            if (buffs == null) return 0f;
            switch (statType)
            {
                case StatType.Attack: return buffs.attackPercent;
                case StatType.Defense: return buffs.defensePercent;
                case StatType.Hp: return buffs.hpPercent;
                case StatType.MoveSpeed: return buffs.speedPercent;
                default: return 0f;
            }
        }

        private GameObject CreateText(string name, RectTransform parent, string text, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = text;
            return go;
        }

        private Text CreateTextComponent(string name, RectTransform parent, Font font, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            return t;
        }

        private string GetQualityLabel(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return "菜鸟";
                case CatQuality.Blue: return "老手";
                case CatQuality.Purple: return "精英";
                case CatQuality.Gold: return "大师";
                default: return "";
            }
        }

        private Color GetQualityTextColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return new Color(0.85f, 0.85f, 0.85f);
                case CatQuality.Blue: return new Color(0.4f, 0.6f, 1f);
                case CatQuality.Purple: return new Color(0.7f, 0.4f, 1f);
                case CatQuality.Gold: return new Color(1f, 0.75f, 0.2f);
                default: return Color.white;
            }
        }

        private Color GetQualityBgColor(CatQuality quality)
        {
            switch (quality)
            {
                case CatQuality.White: return new Color(0.25f, 0.25f, 0.28f);
                case CatQuality.Blue: return new Color(0.15f, 0.25f, 0.45f);
                case CatQuality.Purple: return new Color(0.28f, 0.15f, 0.42f);
                case CatQuality.Gold: return new Color(0.42f, 0.3f, 0.08f);
                default: return new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private void OnDestroy()
        {
            if (_portraitHandleBefore.IsValid()) Addressables.Release(_portraitHandleBefore);
            if (_portraitHandleAfter.IsValid()) Addressables.Release(_portraitHandleAfter);
        }

        #endregion
    }
}

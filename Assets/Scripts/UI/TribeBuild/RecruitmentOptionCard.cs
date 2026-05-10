using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 招募选项卡片组件
    /// </summary>
    public class RecruitmentOptionCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _typeText;
        [SerializeField] private Text _descText;
        [SerializeField] private Button _okButton;

        public RecruitmentOption Option { get; set; }
        public int Index { get; set; }
        public Image BackgroundImage
        {
            get => _backgroundImage;
            set => _backgroundImage = value;
        }

        private AsyncOperationHandle<Sprite> _portraitHandle;

        public void Setup(RecruitmentOption option, int index, Action<RecruitmentOption> onSelected)
        {
            Option = option;
            Index = index;

            if (option.optionType == ChoiceCategory.Affix && option.affixData != null)
            {
                SetupAffixDisplay(option.affixData);
            }
            else
            {
                SetupDefaultDisplay(option);
            }

            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(() => onSelected?.Invoke(option));
            }
        }

        private void SetupAffixDisplay(AffixData affix)
        {
            // Name: 显示词缀名称
            if (_titleText != null)
                _titleText.text = affix.displayName;

            // Type: 显示影响对象
            if (_typeText != null)
                _typeText.text = GetAffixScopeText(affix);

            // Description: 显示词缀效果
            if (_descText != null)
                _descText.text = affix.description;

            // 加载对应图片
            LoadAffixPortrait(affix);
        }

        private void SetupDefaultDisplay(RecruitmentOption option)
        {
            if (_titleText != null)
                _titleText.text = GetDisplayTitle(option);
            if (_typeText != null)
                _typeText.text = GetOptionTypeTitle(option);
            if (_descText != null)
                _descText.text = option.description;
        }

        private string GetAffixScopeText(AffixData affix)
        {
            if (affix.fighterId == 0)
            {
                return "所有猫咪";
            }

            var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(affix.fighterId);
            if (fighterConfig != null)
            {
                return fighterConfig.fighterName;
            }

            return $"兵种{affix.fighterId}";
        }

        private void LoadAffixPortrait(AffixData affix)
        {
            if (_portraitImage == null) return;

            if (affix.fighterId == 0)
            {
                // 所有猫咪 → 使用通用猫神图片
                LoadSpriteByAddress("ui/sprite/buildcard/zhujiemian_img_maoshen");
            }
            else
            {
                // 特定兵种 → 使用该兵种的头像
                var fighterConfig = TribeConfigLoader.Instance?.GetFighterConfig(affix.fighterId);
                if (fighterConfig != null && !string.IsNullOrEmpty(fighterConfig.avatarId))
                {
                    LoadSpriteByAddress($"avatartemp/{fighterConfig.avatarId}1");
                }
            }
        }

        private void LoadSpriteByAddress(string address)
        {
            if (_portraitHandle.IsValid())
                Addressables.Release(_portraitHandle);

            _portraitHandle = Addressables.LoadAssetAsync<Sprite>(address);
            _portraitHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && _portraitImage != null)
                    _portraitImage.sprite = op.Result;
            };
        }

        private void OnDestroy()
        {
            if (_portraitHandle.IsValid())
                Addressables.Release(_portraitHandle);
        }

        private string GetOptionTypeTitle(RecruitmentOption option)
        {
            switch (option.optionType)
            {
                case ChoiceCategory.Reinforcement: return "招募";
                case ChoiceCategory.AddCats: return "繁育";
                case ChoiceCategory.QualityEvolution: return "品质";
                case ChoiceCategory.Buff: return "属性";
                case ChoiceCategory.Affix: return "词缀";
                default: return "招募选项";
            }
        }

        private string GetDisplayTitle(RecruitmentOption option)
        {
            // 有 gameChoice 时用 displayName（光环buff的auraName）
            if (option.gameChoice != null && !string.IsNullOrEmpty(option.gameChoice.displayName))
                return option.gameChoice.displayName;
            return GetOptionTypeTitle(option);
        }
    }
}

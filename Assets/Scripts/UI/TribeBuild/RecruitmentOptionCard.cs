using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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

            if (_titleText != null)
                _titleText.text = GetOptionTypeTitle(option.optionType);
            if (_descText != null)
                _descText.text = option.description;
            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(() => onSelected?.Invoke(option));
            }
        }

        public void SetPortrait(TribeType tribeType)
        {
            if (_portraitImage == null) return;

            string address = GetTribePortraitAddress(tribeType);
            if (string.IsNullOrEmpty(address)) return;

            if (_portraitHandle.IsValid())
                Addressables.Release(_portraitHandle);

            _portraitHandle = Addressables.LoadAssetAsync<Sprite>(address);
            _portraitHandle.Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && _portraitImage != null)
                    _portraitImage.sprite = op.Result;
            };
        }

        private string GetTribePortraitAddress(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Tabby: return "avatartemp/lihua1";
                case TribeType.Orange: return "avatartemp/daju1";
                case TribeType.Cow: return "avatartemp/nainiu1";
                case TribeType.Siamese: return "avatartemp/xianluo1";
                default: return null;
            }
        }

        private void OnDestroy()
        {
            if (_portraitHandle.IsValid())
                Addressables.Release(_portraitHandle);
        }

        private string GetOptionTypeTitle(RecruitmentOptionType optionType)
        {
            switch (optionType)
            {
                case RecruitmentOptionType.NewTribe: return "招募";
                case RecruitmentOptionType.AddCats: return "繁育";
                case RecruitmentOptionType.QualityEvolution: return "品质";
                case RecruitmentOptionType.LeaderBoost: return "属性";
                default: return "招募选项";
            }
        }
    }
}

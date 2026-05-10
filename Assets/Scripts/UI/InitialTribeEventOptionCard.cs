using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 初始兵种选择卡片组件
    /// </summary>
    public class InitialTribeEventOptionCard : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _descText;
        [SerializeField] private Text _countText;
        [SerializeField] private Button _okButton;

        private AsyncOperationHandle<Sprite> _iconHandle;
        public int FighterId { get; private set; }
        public TribeType TribeType { get; private set; }

        /// <summary>
        /// 按 FighterConfig 设置卡片（新版本）
        /// </summary>
        public void SetupByFighter(FighterConfig fighter, Action<int> onSelected)
        {
            FighterId = fighter.fighterId;
            TribeType = (TribeType)fighter.tribeType;

            if (_titleText != null)
                _titleText.text = fighter.fighterName;

            if (_descText != null)
                _descText.text = $"攻击:{fighter.attack} 防御:{fighter.defense} 血量:{fighter.hp}";

            if (_countText != null)
                _countText.text = "";

            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(() => onSelected?.Invoke(fighter.fighterId));
            }

            LoadFighterIcon(fighter);
        }

        private void LoadFighterIcon(FighterConfig fighter)
        {
            if (_iconImage == null) return;

            if (string.IsNullOrEmpty(fighter.avatarId)) return;

            string address = $"avatartemp/{fighter.avatarId}1";
            _iconHandle = Addressables.LoadAssetAsync<Sprite>(address);
            _iconHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    _iconImage.sprite = handle.Result;
            };
        }

        public void SetSelected(bool selected)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = selected
                    ? new Color(1f, 0.9f, 0.3f, 1f)
                    : GetTribeColor(TribeType);
        }

        private Color GetTribeColor(TribeType type)
        {
            return type switch
            {
                TribeType.Tabby => new Color(0.6f, 0.4f, 0.3f, 1f),
                TribeType.Orange => new Color(0.7f, 0.5f, 0.2f, 1f),
                TribeType.Cow => new Color(0.4f, 0.4f, 0.5f, 1f),
                TribeType.Siamese => new Color(0.5f, 0.4f, 0.6f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        private void OnDestroy()
        {
            if (_iconHandle.IsValid())
                Addressables.Release(_iconHandle);
        }
    }
}

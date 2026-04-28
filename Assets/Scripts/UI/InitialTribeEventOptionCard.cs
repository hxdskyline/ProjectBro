using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TribeSystem;

namespace TribeSystem.UI
{
    /// <summary>
    /// 初始族群选择卡片组件
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
        public TribeType TribeType { get; private set; }

        public void Setup(TribeType tribeType, Action<TribeType> onSelected)
        {
            TribeType = tribeType;

            var config = TribeConfigLoader.Instance?.GetTribeConfig(tribeType);

            if (_titleText != null)
                _titleText.text = config != null ? config.tribeName : tribeType.ToString();

            if (_descText != null)
                _descText.text = config != null && !string.IsNullOrEmpty(config.description)
                    ? config.description : "???";

            if (_countText != null)
                _countText.text = config != null ? $"小猫:{config.initialCatCount}只" : "";

            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(() => onSelected?.Invoke(tribeType));
            }

            LoadIcon(tribeType);
        }

        private void LoadIcon(TribeType tribeType)
        {
            if (_iconImage == null) return;

            string breed = GetTribeBreedName(tribeType);
            if (string.IsNullOrEmpty(breed)) return;

            string address = $"avatartemp/{breed}1";
            _iconHandle = Addressables.LoadAssetAsync<Sprite>(address);
            _iconHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    _iconImage.sprite = handle.Result;
            };
        }

        private string GetTribeBreedName(TribeType tribeType)
        {
            return tribeType switch
            {
                TribeType.Tabby => "lihua",
                TribeType.Orange => "daju",
                TribeType.Cow => "nainiu",
                TribeType.Siamese => "xianluo",
                _ => null
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

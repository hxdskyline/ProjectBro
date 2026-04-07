using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TribeSystem.UI
{
    /// <summary>
    /// 族群卡片组件 - 显示单个族群信息
    /// </summary>
    public class TribeCard : MonoBehaviour
    {
        [Header("UI 组件")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _countText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _statsText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _portraitImage; // 族群头像
        [SerializeField] private Toggle _toggle;
        [SerializeField] private Button _detailButton; // 点击显示详情的按钮

        private TribeRecord _tribe;
        private bool _isDeployed;
        private System.Action<int, bool> _onToggleChanged;
        private System.Action<TribeRecord, bool> _onShowDetail;
        private int _currentVariant = 1;
        private TribeType _tribeType;
        private AsyncOperationHandle<Sprite> _portraitHandle;

        /// <summary>
        /// 设置卡片数据
        /// </summary>
        public void Setup(TribeRecord tribe, bool isDeployed, System.Action<int, bool> onToggleChanged, System.Action<TribeRecord, bool> onShowDetail = null)
        {
            _tribe = tribe;
            _isDeployed = isDeployed;
            _onToggleChanged = onToggleChanged;
            _onShowDetail = onShowDetail;

            // 绑定 Toggle 事件
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveAllListeners();
                _toggle.onValueChanged.AddListener(OnToggleValueChanged);
                _toggle.SetIsOnWithoutNotify(isDeployed);
            }

            // 绑定详情按钮事件
            if (_detailButton != null)
            {
                _detailButton.onClick.RemoveAllListeners();
                _detailButton.onClick.AddListener(OnDetailButtonClicked);
            }

            // 更新背景颜色
            if (_backgroundImage != null)
            {
                _backgroundImage.color = GetTribeCardColor(tribe.tribeType);
            }

            // 加载头像
            _tribeType = tribe.tribeType;
            _currentVariant = 1;
            LoadPortrait(_tribeType, _currentVariant);

            // 更新文本内容
            UpdateTexts();
        }

        /// <summary>
        /// 切换头像 variant
        /// </summary>
        public void SetPortraitVariant(int variant)
        {
            if (_currentVariant == variant) return;
            _currentVariant = variant;
            LoadPortrait(_tribeType, _currentVariant);
        }

        private void LoadPortrait(TribeType tribeType, int variant)
        {
            if (_portraitImage == null) return;

            string address = GetTribePortraitAddress(tribeType, variant);
            if (!string.IsNullOrEmpty(address))
            {
                if (_portraitHandle.IsValid())
                {
                    Addressables.Release(_portraitHandle);
                }
                _portraitHandle = Addressables.LoadAssetAsync<Sprite>(address);
                _portraitHandle.Completed += (op) =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded && _portraitImage != null)
                    {
                        _portraitImage.sprite = op.Result;
                    }
                };
            }
        }

        private string GetTribePortraitAddress(TribeType tribeType, int variant)
        {
            switch (tribeType)
            {
                case TribeType.Maine: return $"avatartemp/mianyin{variant}";
                case TribeType.Tabby: return $"avatartemp/lihua{variant}";
                case TribeType.Orange: return $"avatartemp/daju{variant}";
                case TribeType.Cow: return $"avatartemp/nainiu{variant}";
                case TribeType.Siamese: return $"avatartemp/xianluo{variant}";
                case TribeType.Ragdoll: return $"avatartemp/buou{variant}";
                default: return null;
            }
        }

        private void UpdateTexts()
        {
            if (_tribe == null) return;

            // 族群名称
            if (_nameText != null)
            {
                _nameText.text = GetTribeTypeName(_tribe.tribeType);
            }

            // 小猫数量
            if (_countText != null)
            {
                _countText.text = $"小猫: {_tribe.GetCatCount()} 只";
            }

            // 状态
            if (_statusText != null)
            {
                if (_tribe.IsLeaderResting())
                {
                    _statusText.text = $"族长休息中 ({_tribe.leader.restTurns}回)";
                    _statusText.color = new Color(1f, 0.3f, 0.3f, 1f);
                }
                else
                {
                    _statusText.text = "状态: 可出战";
                    _statusText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                }
            }

            // 族长属性
            if (_statsText != null)
            {
                _statsText.text = $"攻{_tribe.leader.baseAttack} 防{_tribe.leader.baseDefense}\n血{_tribe.leader.baseHp} 速{_tribe.leader.baseSpeed} 统{_tribe.leader.command}";
            }
        }

        private void OnToggleValueChanged(bool value)
        {
            if (_tribe != null && _onToggleChanged != null)
            {
                _onToggleChanged(_tribe.tribeId, value);
            }
        }

        private void OnDetailButtonClicked()
        {
            if (_tribe != null && _onShowDetail != null)
            {
                _onShowDetail(_tribe, _isDeployed);
            }
        }

        private Color GetTribeCardColor(TribeType tribeType)
        {
            switch (tribeType)
            {
                case TribeType.Maine: return new Color(0.3f, 0.5f, 0.7f, 1f);
                case TribeType.Tabby: return new Color(0.6f, 0.4f, 0.3f, 1f);
                case TribeType.Orange: return new Color(0.7f, 0.5f, 0.2f, 1f);
                case TribeType.Cow: return new Color(0.4f, 0.4f, 0.5f, 1f);
                case TribeType.Siamese: return new Color(0.5f, 0.4f, 0.6f, 1f);
                case TribeType.Ragdoll: return new Color(0.7f, 0.5f, 0.6f, 1f);
                default: return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (_portraitHandle.IsValid())
            {
                Addressables.Release(_portraitHandle);
            }
        }

        private string GetTribeTypeName(TribeType type)
        {
            switch (type)
            {
                case TribeType.Maine: return "缅因";
                case TribeType.Tabby: return "狸花";
                case TribeType.Orange: return "大橘";
                case TribeType.Cow: return "奶牛";
                case TribeType.Siamese: return "暹罗";
                case TribeType.Ragdoll: return "布偶";
                default: return type.ToString();
            }
        }
    }
}

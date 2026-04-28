using System;
using System.Collections.Generic;

namespace TribeSystem
{
    /// <summary>
    /// 静态卡牌模板 — 描述"一张卡是什么"，dataId 对应配置文件
    /// 可独立用于图鉴/牌库展示
    /// </summary>
    [Serializable]
    public class Card
    {
        /// <summary>静态唯一标识，对应 JSON 配置（如 "tabby_leader"）</summary>
        public string dataId;

        /// <summary>显示名</summary>
        public string displayName;

        /// <summary>描述</summary>
        public string description;

        /// <summary>图标资源地址</summary>
        public string iconAddress;

        /// <summary>所属族群</summary>
        public TribeType tribeType;

        /// <summary>基础属性</summary>
        public int baseAttack;
        public int baseDefense;
        public int baseHp;
        public float baseMoveSpeed;

        /// <summary>固有特殊 buff（天生被动）</summary>
        public List<TribeBuff> innateBuffs;

        public Card()
        {
            dataId = "";
            displayName = "";
            description = "";
            iconAddress = "";
            tribeType = TribeType.None;
            baseAttack = 0;
            baseDefense = 0;
            baseHp = 0;
            baseMoveSpeed = 0f;
            innateBuffs = new List<TribeBuff>();
        }
    }
}

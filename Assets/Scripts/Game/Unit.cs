using UnityEngine;
using LitJson;

/// <summary>
/// 兵种基类 - 所有兵种都继承这个类
/// </summary>
public class Unit : MonoBehaviour
{
    [SerializeField] protected int _unitId;
    [SerializeField] protected string _unitName;
    [SerializeField] protected int _level;
    [SerializeField] protected int _star;
    [SerializeField] protected int _hp;
    [SerializeField] protected int _maxHp;
    [SerializeField] protected int _attack;
    [SerializeField] protected int _defense;

    public int UnitId => _unitId;
    public string UnitName => _unitName;
    public int Level => _level;
    public int Star => _star;
    public int HP => _hp;
    public int MaxHP => _maxHp;
    public int Attack => _attack;
    public int Defense => _defense;

    /// <summary>
    /// 初始化兵种
    /// </summary>
    public virtual void Initialize(int unitId, int level, int star)
    {
        _unitId = unitId;
        _level = level;
        _star = star;

        // 从数据表加载兵种基本数据
        JsonData unitData = TableReader.Instance.GetRecord("Units", unitId.ToString());
        if (unitData != null)
        {
            _unitName = unitData["name"].ToString();
            _attack = int.Parse(unitData["attack"].ToString());
            _defense = int.Parse(unitData["defense"].ToString());
            _maxHp = int.Parse(unitData["hp"].ToString());
            
            // 根据等级和星级调整属性
            ApplyLevelAndStarBonus();
        }

        _hp = _maxHp;
    }

    /// <summary>
    /// 应用等级和星级的属性加成
    /// </summary>
    protected virtual void ApplyLevelAndStarBonus()
    {
        // 简单的加成公式，可以根据需要修改
        float levelBonus = 1 + (_level - 1) * 0.1f;
        float starBonus = 1 + _star * 0.2f;
        float totalBonus = levelBonus * starBonus;

        _maxHp = (int)(_maxHp * totalBonus);
        _attack = (int)(_attack * totalBonus);
        _defense = (int)(_defense * totalBonus);
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    public virtual void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - _defense);
        _hp -= actualDamage;
        
        if (_hp < 0)
            _hp = 0;

        Debug.Log($"[Unit] {_unitName} took {actualDamage} damage. HP: {_hp}/{_maxHp}");
    }

    /// <summary>
    /// 恢复HP
    /// </summary>
    public virtual void Heal(int amount)
    {
        _hp = Mathf.Min(_maxHp, _hp + amount);
    }

    /// <summary>
    /// 检查是否死亡
    /// </summary>
    public virtual bool IsDead()
    {
        return _hp <= 0;
    }
}
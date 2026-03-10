using UnityEngine;

/// <summary>
/// 卡牌构筑使用的轻量卡牌数据。
/// </summary>
[System.Serializable]
public struct CardBuildCardData
{
    public int Id;
    public string Name;
    public string Gender;
    public int Attack;
    public int Defense;
    public int Hp;
    public float MoveSpeed;
    public float AttackRange;

    public int GetBattlePower()
    {
        int attackScore = Mathf.Max(1, Attack) * 4;
        int defenseScore = Mathf.Max(0, Defense) * 3;
        int hpScore = Mathf.Max(1, Hp);
        int moveSpeedScore = Mathf.RoundToInt(Mathf.Max(0.1f, MoveSpeed) * 10f);
        int attackRangeScore = Mathf.RoundToInt(Mathf.Max(0.1f, AttackRange) * 12f);
        return attackScore + defenseScore + hpScore + moveSpeedScore + attackRangeScore;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [HideInInspector] public string playerName;
    [HideInInspector] public int id;
    [HideInInspector] public int HP, armature;
    [HideInInspector] public int gold;
    [HideInInspector] public bool isDie;

    /// <summary>本回合累计的击杀/助攻奖励，将在下回合开始（Preparation）时一并发放并清零。</summary>
    [HideInInspector] public int pendingKillReward;

    /// <summary>
    /// 本次生命周期内对自己造成过伤害的所有攻击者名字（用于死亡时结算助攻）。
    /// Reborn 时清空。
    /// </summary>
    private readonly HashSet<string> damageContributors = new();

    void Start()
    {
        HP = 100;
        armature = 0;
    }

    public void GetDamaged(string attackerName, int damage, bool shotHead, int weaponId)
    {
        if (attackerName == this.playerName)
            return;

        // 记录这次伤害的来源（用于死亡时分发助攻）
        damageContributors.Add(attackerName);

        // 护甲先吸收，剩余伤害扣 HP
        if (damage <= armature)
        {
            armature -= damage;
        }
        else
        {
            int remaining = damage - armature;
            armature = 0;
            HP -= remaining;
        }
        HP = Mathf.Max(HP, 0);
        if(HP == 0 && !isDie)
        {
            isDie = true;
            MatchManager.instance.aliveNum[id < 3 ? 0 : 1]--;

            NetworkManager.playerDieList.Add(this.playerName);
            PlayerKill playerKill = new(attackerName, this.playerName, shotHead, weaponId);
            NetworkManager.Broadcast(MessageType.Kill, playerKill);

            // 分发奖励：最后一击拿 KILL_REWARD，其余贡献者拿 ASSIST_REWARD
            DistributeKillRewards(killerName: attackerName);

            if (NetworkManager.playerPool.TryGetValue(this.playerName, out var player) && player != null)
                player.SetActive(false);
        }
    }

    /// <summary>
    /// 把击杀奖励 + 助攻奖励累计到对应玩家的 pendingKillReward。
    /// 下回合 GrantRoundIncome 时统一发放。
    /// </summary>
    private void DistributeKillRewards(string killerName)
    {
        foreach (string contributor in damageContributors)
        {
            int reward = (contributor == killerName)
                ? MatchManager.KILL_REWARD
                : MatchManager.ASSIST_REWARD;

            if (NetworkManager.playerPool.TryGetValue(contributor, out var go) && go != null)
            {
                var state = go.GetComponent<PlayerState>();
                if (state != null) state.pendingKillReward += reward;
            }
        }
    }

    public void Initialize(int id, string playerName)
    {
        this.id = id;
        this.playerName = playerName;
        gold = MatchManager.INITIAL_GOLD;       // 开局发钱
        pendingKillReward = 0;
    }

    public void Reborn()
    {
        HP = 100;
        armature = 0;
        isDie = false;
        damageContributors.Clear();             // 新一回合，伤害贡献者清零
    }

    /// <summary>
    /// 回合切换到 Preparation 时调用：发本回合基础金币 + 上回合累计的击杀/助攻奖励，并清零暂存。
    /// </summary>
    public void GrantRoundIncome()
    {
        gold += MatchManager.ROUND_INCOME + pendingKillReward;
        pendingKillReward = 0;
    }

    public void UpdateStateInfo(PlayerStateInfo state)
    {
        state.HP = HP;
        state.armature = armature;
        state.gold = gold;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [HideInInspector] public int uid;
    [HideInInspector] public int slot;
    [HideInInspector] public int team;
    [HideInInspector] public int HP, armor;
    [HideInInspector] public int gold;
    [HideInInspector] public bool isDie;

    [HideInInspector] public int pendingKillReward;

    private readonly HashSet<int> damageContributors = new();

    void Start()
    {
        HP = 100;
        armor = 0;
    }

    public void GetDamaged(int attackerUid, int damage, bool shotHead, int weaponId)
    {
        if (attackerUid == this.uid)
            return;

        // 记录这次伤害的来源（用于死亡时分发助攻）
        damageContributors.Add(attackerUid);

        // 护甲先吸收，剩余伤害扣 HP
        if (damage <= armor)
        {
            armor -= damage;
        }
        else
        {
            int remaining = damage - armor;
            armor = 0;
            HP -= remaining;
        }
        HP = Mathf.Max(HP, 0);
        if(HP == 0 && !isDie)
        {
            isDie = true;
            MatchManager.instance.aliveNum[team]--;

            if (NetworkManager.players.TryGetValue(this.uid, out var entity))
                entity.isDead = true;

            PlayerKill playerKill = new(attackerUid, this.uid, shotHead, weaponId);
            NetworkManager.Broadcast(MessageType.Kill, playerKill);

            // 分发奖励：最后一击拿 KILL_REWARD，其余贡献者拿 ASSIST_REWARD
            DistributeKillRewards(killerUid: attackerUid);

            if (NetworkManager.players.TryGetValue(this.uid, out var entity2) && entity2.root != null)
                entity2.root.SetActive(false);
        }
    }

    /// <summary>
    /// 把击杀奖励 + 助攻奖励累计到对应玩家的 pendingKillReward。
    /// 下回合 GrantRoundIncome 时统一发放。
    /// </summary>
    private void DistributeKillRewards(int killerUid)
    {
        foreach (int contributor in damageContributors)
        {
            int reward = (contributor == killerUid)
                ? MatchManager.KILL_REWARD
                : MatchManager.ASSIST_REWARD;

            if (NetworkManager.players.TryGetValue(contributor, out var entity) && entity.state != null)
                entity.state.pendingKillReward += reward;
        }
    }

    public void Initialize(int slot, int uid)
    {
        this.slot = slot;
        this.uid = uid;
        team = slot < 3 ? 0 : 1;
        gold = MatchManager.INITIAL_GOLD;     
        pendingKillReward = 0;
    }

    public void Reborn()
    {
        HP = 100;
        armor = 0;
        isDie = false;
        damageContributors.Clear(); 
    }

    public void GrantRoundIncome()
    {
        gold += MatchManager.ROUND_INCOME + pendingKillReward;
        pendingKillReward = 0;
    }

    public void UpdateStateInfo(PlayerStateInfo state)
    {
        state.HP = HP;
        state.armor = armor;
        state.gold = gold;
    }
}

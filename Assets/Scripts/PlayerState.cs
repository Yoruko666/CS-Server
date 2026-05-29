using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [HideInInspector] public string playerName;
    [HideInInspector] public int id;
    [HideInInspector] public int HP, armature;
    [HideInInspector] public int gold;
    [HideInInspector] public bool isDie;

    /// <summary>本回合累计的击杀奖励，将在下回合开始（Preparation）时一并发放并清零。</summary>
    [HideInInspector] public int pendingKillReward;

    void Start()
    {
        HP = 100;
        armature = 0;
    }

    public void GetDamaged(string attackerName, int damage, bool shotHead, int weaponId)
    {
        if (attackerName == this.playerName)
            return;

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

            // 击杀奖励：累计到攻击者的 pendingKillReward，下回合统一发放
            if (NetworkManager.playerPool.TryGetValue(attackerName, out var attacker) && attacker != null)
            {
                var attackerState = attacker.GetComponent<PlayerState>();
                if (attackerState != null)
                    attackerState.pendingKillReward += MatchManager.KILL_REWARD;
            }

            if (NetworkManager.playerPool.TryGetValue(this.playerName, out var player) && player != null)
                player.SetActive(false);
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
    }

    /// <summary>
    /// 回合切换到 Preparation 时调用：发本回合基础金币 + 上回合累计的击杀奖励，并清零暂存。
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

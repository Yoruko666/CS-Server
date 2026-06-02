using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MatchManager : SingletonMono<MatchManager>
{
    // ============ 经济参数 ============
    /// <summary>开局给每个玩家的初始金币。</summary>
    public const int INITIAL_GOLD = 300;
    /// <summary>每回合开始时固定发的金币（不含击杀/助攻奖励）。</summary>
    public const int ROUND_INCOME = 300;
    /// <summary>击杀一个敌人累计的奖励，下回合发放。</summary>
    public const int KILL_REWARD = 200;
    /// <summary>助攻奖励：对死者造成过伤害但不是最后一击。</summary>
    public const int ASSIST_REWARD = 100;

    private bool gameStart = false;
    private readonly int ROUND_TO_WIN = 10;
    private Dictionary<RoundState, float> round_time;

    private int[] score = new int[2];
    public int[] aliveNum = new int[2];
    private int currentRound;
    [HideInInspector] public RoundState currentRoundState;
    private float roundTimer;

    public MapConfig mapConfig;

    protected override void OnSingletonAwake()
    {
        round_time = new();
        round_time.Add(RoundState.Preparation, 5f);
        round_time.Add(RoundState.InProgress, 180f);
        round_time.Add(RoundState.RoundOver, 5f);
    }

    void Start()
    {
        currentRound = 1;
        currentRoundState = RoundState.Preparation;
    }

    void Update()
    {
        if (!gameStart)
        {
            if (NetworkManager.players.Values.All(e => e.isReady) && NetworkManager.serverReady)
                StartGame();
            else return;
        }
        roundTimer -= Time.deltaTime;
        switch (currentRoundState)
        {
            case RoundState.Preparation:
                if (roundTimer <= 0)
                {
                    SwitchProgress(RoundState.InProgress);
                }
                break;
            case RoundState.InProgress:
                if (roundTimer <= 0 || RoundEnd())
                {
                    int winTeam = 0;
                    if(aliveNum[0] < aliveNum[1]) winTeam = 1;
                    score[winTeam]++;

                    RoundEnd win = new(winTeam);
                    NetworkManager.Broadcast(MessageType.RoundEnd, win);

                    SwitchProgress(RoundState.RoundOver);
                }
                break;
            case RoundState.RoundOver:
                if (roundTimer <= 0)
                {
                    currentRound++;
                    StartNextRound();
                    SwitchProgress(RoundState.Preparation);
                }
                break;
        }
    }

    public void StartGame()
    {
        gameStart = true;
        InitializeFirstRound();
        SwitchProgress(RoundState.Preparation);
        List<PlayerStateInfo> allPlayersInfo = NetworkManager.players.Values.Select(e => e.stateInfo).ToList();
        NetworkManager.Broadcast(MessageType.Start, allPlayersInfo);
    }

    public void SwitchProgress(RoundState progress)
    {
        roundTimer = round_time[progress];
        currentRoundState = progress;
        GameProgress gameProgress = new(progress);
        NetworkManager.Broadcast(MessageType.GameProgress, gameProgress);
    }

    public bool RoundEnd()
    {
        return aliveNum[0] == 0 || aliveNum[1] == 0;
    }

    /// <summary>
    /// 整局游戏第一次开局：玩家 PlayerState.Initialize 已经在 NetworkManager.Start 阶段发了 INITIAL_GOLD。
    /// 这里负责重置存活数 + 复活所有玩家 + 武器/血量初始化。
    /// </summary>
    private void InitializeFirstRound()
    {
        aliveNum[0] = NetworkManager.playerNum / 2;
        aliveNum[1] = NetworkManager.playerNum / 2;
        foreach (var entity in NetworkManager.players.Values)
        {
            if (entity.root == null) continue;
            entity.root.SetActive(true);
            entity.controller.Reborn();
            // 首回合：所有人都没有主武器，按 dropMainGun=true 走（其实初始 weapons[1]=null，效果一样）
            entity.weapon.InitializeForRound(dropMainGun: true);
            entity.state.Reborn();
        }
    }

    /// <summary>
    /// 第二回合及之后：发放金币（基础 + 击杀奖励）、按死亡名单决定武器去留、补满弹药、复活所有人。
    /// </summary>
    private void StartNextRound()
    {
        aliveNum[0] = NetworkManager.playerNum / 2;
        aliveNum[1] = NetworkManager.playerNum / 2;

        foreach (var entity in NetworkManager.players.Values)
        {
            if (entity.root == null) continue;

            bool died = entity.isDead;

            // 1) 武器：死了丢主武器，活着保留并补满弹药
            entity.weapon.InitializeForRound(dropMainGun: died);

            // 2) 经济：发本回合基础金币 + 上回合累计击杀奖励
            entity.state.GrantRoundIncome();

            // 3) 复活 + 重置 HP / 移动状态
            entity.root.SetActive(true);
            entity.controller.Reborn();
            entity.state.Reborn();

            // 4) 清除死亡标记
            entity.isDead = false;
        }
    }
}

public enum RoundState
{
    Preparation, InProgress, RoundOver
}

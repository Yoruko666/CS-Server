using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MatchManager : MonoBehaviour
{
    public static MatchManager instance;

    private bool gameStart = false;
    private readonly int ROUND_TO_WIN = 10;
    private Dictionary<RoundState, float> round_time;

    private int[] score = new int[2];
    public int[] aliveNum = new int[2];
    private int currentRound;
    [HideInInspector] public RoundState currentRoundState;
    private float roundTimer;

    public MapConfig mapConfig;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);

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
            if (NetworkManager.playerReadyList.Count == NetworkManager.playerNum && NetworkManager.serverReady)
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
                    Initialize();
                    SwitchProgress(RoundState.Preparation);
                }
                break;
        }
    }

    public void StartGame()
    {
        gameStart = true;
        Initialize();
        SwitchProgress(RoundState.Preparation);
        List<PlayerStateInfo> allPlayersInfo = NetworkManager.playerStateInfos.Values.ToList();
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

    public void Initialize()
    {
        aliveNum[0] = NetworkManager.playerNum / 2;
        aliveNum[1] = NetworkManager.playerNum / 2;
        foreach (string playerName in NetworkManager.playerStateInfos.Keys)
        {
            if (!NetworkManager.playerPool.TryGetValue(playerName, out var player) || player == null) continue;
            player.SetActive(true);
            player.GetComponent<PlayerController>().Reborn();
            player.GetComponent<WeaponManager>().Initialize();
            player.GetComponent<PlayerState>().Reborn();
        }
        NetworkManager.playerDieList.Clear();
    }
}

public enum RoundState
{
    Preparation, InProgress, RoundOver
}
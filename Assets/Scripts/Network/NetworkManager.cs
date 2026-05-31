using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    //Shared
    private float tickTimer;
    public readonly static float TICK_INTERVAL = 1f / 128f;

    //Server specific
    private static UdpClient udpServer;

    public static int playerNum = 0;
    public static bool serverReady = false;

    public static ConcurrentDictionary<string, IPEndPoint> clientsDic = new();

    private static ConcurrentQueue<(MessageType, string)> messageList = new();
    public static ConcurrentDictionary<string, GameObject> playerPool = new();
    public static ConcurrentDictionary<int, GameObject> slotPlayerPool = new();
    public static ConcurrentDictionary<string, PlayerStateInfo> playerStateInfos = new();

    public static HashSet<string> playerReadyList = new();
    public static HashSet<string> playerDieList = new();

    // 接收线程的 running flag：OnApplicationQuit 时翻为 false，
    // 让 ReceiveMessage 线程能干净退出，而不是依赖 Receive 抛 ObjectDisposedException 跳出。
    private static volatile bool running = true;

    // 消息处理器注册表
    private Dictionary<MessageType, Action<string>> handlers;

    private void Awake()
    {
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        handlers = new Dictionary<MessageType, Action<string>>
        {
            { MessageType.Ready,           OnReady          },
            { MessageType.InputInfo,       OnInputInfo      },
            { MessageType.Fire,            OnFire           },
            { MessageType.Reload,          OnReload         },
            { MessageType.SwitchWeapon,    OnSwitchWeapon   },
            { MessageType.PurchaseWeapon,  OnPurchaseWeapon },
            { MessageType.PingPong,        OnPingPong       },
        };
    }

    void Start()
    {
        int port = 25001;
        string[] args = Environment.GetCommandLineArgs();
        for(int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                    port = int.Parse(args[i + 1]);
                    break;

                case "-l":
                    List<string> playerList = JsonConvert.DeserializeObject<List<string>>(args[i + 1]);
                    playerNum = playerList.Count;
                    int[] j2id = new int[] { 0, 3, 1, 4, 2, 5 }; 
                    for(int j = 0; j < playerNum; j++)
                    {
                        string uidStr = playerList[j];
                        int uid = int.Parse(uidStr);
                        playerStateInfos.TryAdd(uidStr, new PlayerStateInfo());
                        playerStateInfos[uidStr].Initialize(uid);
                        int slot = j2id[j];
                        playerStateInfos[uidStr].slot = slot;
                        playerStateInfos[uidStr].team = slot < 3 ? 0 : 1;
                        GameObject player = Instantiate(Resources.Load<GameObject>("Prefabs/Character"));
                        player.GetComponent<PlayerController>().Initialize(slot, uid);
                        player.GetComponent<PlayerState>().Initialize(slot, uid);
                        player.GetComponent<WeaponManager>().uid = uid;
                        playerPool.TryAdd(uidStr, player);
                        slotPlayerPool.TryAdd(slot, player);
                    }
                    break;
            }
        }

        SceneManager.LoadScene("Map", LoadSceneMode.Additive);
        udpServer = new UdpClient(port);
        Debug.Log("Server is working...");
        Thread recvThread = new(new ThreadStart(ReceiveMessage));
        recvThread.Start();
        serverReady = true;
    }

    private void Update()
    {
        while (messageList.TryDequeue(out var data))
        {
            if (handlers.TryGetValue(data.Item1, out var handler))
            {
                try { handler(data.Item2); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                Debug.LogWarning($"No handler for message type: {data.Item1}");
            }
        }

        tickTimer += Time.deltaTime;
        while (tickTimer >= TICK_INTERVAL)
        {
            tickTimer -= TICK_INTERVAL;
            TickSimulation();
        }
    }

    /// <summary>每 tick 模拟所有玩家 + 广播全员状态</summary>
    private void TickSimulation()
    {
        foreach (string uidStr in playerStateInfos.Keys)
        {
            if (!playerPool.TryGetValue(uidStr, out var player) || player == null) continue;
            player.GetComponent<PlayerState>().UpdateStateInfo(playerStateInfos[uidStr]);
            if (playerDieList.Contains(uidStr)) continue;

            var playerController = player.GetComponent<PlayerController>();
            PlayerInputInfo inputInfo = playerController.GetInputInfo();
            playerController.ProcessInput(inputInfo);
            playerController.UpdateStateInfo(playerStateInfos[uidStr]);

            var weaponManager = player.GetComponent<WeaponManager>();
            weaponManager.HandleTick();
            weaponManager.UpdateStateInfo(playerStateInfos[uidStr]);
        }
        List<PlayerStateInfo> allPlayersInfo = playerStateInfos.Values.ToList();
        Broadcast(MessageType.AllPlayersInfo, allPlayersInfo);
    }

    // ============ Message Handlers ============

    private void OnReady(string msg)
    {
        var playerReady = JsonConvert.DeserializeObject<PlayerReady>(msg);
        string uidStr = playerReady.uid.ToString();
        if (!playerReadyList.Contains(uidStr))
            playerReadyList.Add(uidStr);
    }

    private void OnInputInfo(string msg)
    {
        var inputInfo = JsonConvert.DeserializeObject<PlayerInputInfo>(msg);
        string uidStr = inputInfo.uid.ToString();
        if (playerDieList.Contains(uidStr)) return;
        if (playerPool.TryGetValue(uidStr, out var player) && player != null)
            player.GetComponent<PlayerController>().ApplyInput(inputInfo);
    }

    private void OnFire(string msg)
    {
        var playerFire = JsonConvert.DeserializeObject<PlayerFire>(msg);
        string uidStr = playerFire.uid.ToString();
        if (playerDieList.Contains(uidStr)) return;
        if (playerPool.TryGetValue(uidStr, out var player) && player != null)
            player.GetComponent<WeaponManager>().Fire(playerFire.seed);
    }

    private void OnReload(string msg)
    {
        var playerReload = JsonConvert.DeserializeObject<PlayerReload>(msg);
        string uidStr = playerReload.uid.ToString();
        if (!playerDieList.Contains(uidStr)
            && playerPool.TryGetValue(uidStr, out var player) && player != null)
            player.GetComponent<WeaponManager>().StartReload();
        Broadcast(MessageType.Reload, playerReload);
    }

    private void OnSwitchWeapon(string msg)
    {
        var playerSwitchWeapon = JsonConvert.DeserializeObject<PlayerSwitchWeapon>(msg);
        if (playerPool.TryGetValue(playerSwitchWeapon.uid.ToString(), out var player) && player != null)
            player.GetComponent<WeaponManager>().SwitchWeapon(playerSwitchWeapon.index);
        Broadcast(MessageType.SwitchWeapon, playerSwitchWeapon);
    }

    private void OnPurchaseWeapon(string msg)
    {
        if (MatchManager.instance.currentRoundState != RoundState.Preparation) return;
        var playerPurchaseWeapon = JsonConvert.DeserializeObject<PlayerPurchaseWeapon>(msg);
        if (!playerPool.TryGetValue(playerPurchaseWeapon.uid.ToString(), out var player) || player == null) return;
        var playerState = player.GetComponent<PlayerState>();
        if (playerState == null) return;

        WeaponConfig weaponConfig = WeaponDic.instance.weaponDic[playerPurchaseWeapon.id];
        if (playerState.gold < weaponConfig.price) return;

        // 扣权威金币（下一 tick 通过 UpdateStateInfo 同步到客户端）
        playerState.gold -= weaponConfig.price;

        // 服务端武器组件装备
        var weaponManager = player.GetComponent<WeaponManager>();
        weaponManager.AcquireWeapon(weaponConfig.id, weaponConfig.magazineCapacity, weaponConfig.magazineCapacity * 2);

        // 统一通过 AcquireWeapon 广播给所有客户端（包括购买者自己）。
        // 购买者本地不再做任何预测，等这条权威广播到达再装备武器。
        var playerAcquireWeapon = new PlayerAcquireWeapon(playerPurchaseWeapon.uid, weaponConfig.id);
        Broadcast(MessageType.AcquireWeapon, playerAcquireWeapon);
    }

    private void OnPingPong(string msg)
    {
        var pingPong = JsonConvert.DeserializeObject<PingPong>(msg);
        string uidStr = pingPong.uid.ToString();
        if (clientsDic.ContainsKey(uidStr))
            Send(uidStr, MessageType.PingPong, pingPong);
    }

    private void OnApplicationQuit()
    {
        running = false;
        udpServer?.Close();
    }

    private void ReceiveMessage()
    {
        IPEndPoint remote = new(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpServer.Receive(ref remote);

                if (data.Length < 8)
                {
                    Debug.Log("Data is too short.");
                    continue;
                }

                int length = BitConverter.ToInt32(data, 0);
                if (length != data.Length - 4)
                {
                    Debug.Log("Data length mismatch.");
                    continue;
                }

                MessageType type = (MessageType)BitConverter.ToInt32(data, 4);

                string str = Encoding.UTF8.GetString(data, 8, data.Length - 8);
                messageList.Enqueue((type, str));

                // 首次 Ready 消息时登记 endpoint。
                // 必须 clone IPEndPoint，因为 udpServer.Receive(ref remote) 会复用同一个引用。
                if (type == MessageType.Ready)
                {
                    PlayerReady playerReady = JsonConvert.DeserializeObject<PlayerReady>(str);
                    if (playerReady != null)
                    {
                        string uidStr = playerReady.uid.ToString();
                        if (!clientsDic.ContainsKey(uidStr))
                        {
                            var endpointCopy = new IPEndPoint(remote.Address, remote.Port);
                            clientsDic[uidStr] = endpointCopy;
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"recv socket error: {ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    public static void Send<T>(string uid, MessageType type, T data)
    {
        try
        {
            byte[] typeBytes = BitConverter.GetBytes((int)type);
            string dataStr = JsonConvert.SerializeObject(data);
            byte[] dataBytes = Encoding.UTF8.GetBytes(dataStr);
            byte[] lengthBytes = BitConverter.GetBytes(4 + dataBytes.Length);
            byte[] sendBuffer = new byte[8 + dataBytes.Length];
            Buffer.BlockCopy(lengthBytes, 0, sendBuffer, 0, 4);
            Buffer.BlockCopy(typeBytes, 0, sendBuffer, 4, 4);
            Buffer.BlockCopy(dataBytes, 0, sendBuffer, 8, dataBytes.Length);
            udpServer.Send(sendBuffer, sendBuffer.Length, clientsDic[uid]);
        }
        catch (JsonSerializationException ex)
        {
            Debug.Log($"JSON serialize error：{ex.Message}");
        }
        catch (SocketException ex)
        {
            Debug.Log($"UDP send error：{ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.Log($"Message send error：{ex.Message}");
        }
    }

    public static void Broadcast<T>(MessageType type, T data)
    {
        foreach (string uid in clientsDic.Keys)
            Send(uid, type, data);
    }
}
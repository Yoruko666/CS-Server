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

    public static ConcurrentDictionary<int, PlayerEntity> players = new();

    // 消息队列携带 endpoint：OnConnect 需要它来登记客户端地址；
    // 其他 handler 不关心，参数原样接收忽略即可。
    private static ConcurrentQueue<(MessageType type, string body, IPEndPoint remote)> messageList = new();

    private static volatile bool running = true;
    private Dictionary<MessageType, Action<string, IPEndPoint>> handlers;

    /// <summary>
    /// TickSimulation 广播复用的 list，避免每 tick (128Hz) 都 ToList() 新建。
    /// </summary>
    private readonly List<PlayerStateInfo> _broadcastList = new();

    private void Awake()
    {
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        handlers = new Dictionary<MessageType, Action<string, IPEndPoint>>
        {
            { MessageType.Connect,         OnConnect        },
            { MessageType.Ready,           OnReady          },
            { MessageType.InputInfo,       OnInputInfo      },
            { MessageType.Fire,            OnFire           },
            { MessageType.Reload,          OnReload         },
            { MessageType.SwitchWeapon,    OnSwitchWeapon   },
            { MessageType.PurchaseWeapon,  OnPurchaseWeapon },
            { MessageType.PingPong,        OnPingPong       },
            { MessageType.Chat,            OnChat           },
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
                        int uid = int.Parse(playerList[j]);
                        int slot = j2id[j];
                        int team = slot < 3 ? 0 : 1;

                        GameObject player = Instantiate(Resources.Load<GameObject>("Prefabs/Character"));
                        player.GetComponent<PlayerController>().Initialize(slot, uid);
                        player.GetComponent<PlayerState>().Initialize(slot, uid);
                        player.GetComponent<WeaponManager>().uid = uid;

                        var entity = PlayerEntity.Create(player, uid, slot, team);
                        entity.stateInfo.Initialize(uid);
                        entity.stateInfo.slot = slot;
                        entity.stateInfo.team = team;
                        players.TryAdd(uid, entity);
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
            if (handlers.TryGetValue(data.type, out var handler))
            {
                try { handler(data.body, data.remote); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            else
            {
                Debug.LogWarning($"No handler for message type: {data.type}");
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
        foreach (int uid in players.Keys)
        {
            if (!players.TryGetValue(uid, out var entity) || entity.root == null) continue;
            entity.state.UpdateStateInfo(entity.stateInfo);
            if (entity.isDead) continue;

            PlayerInputInfo inputInfo = entity.controller.GetInputInfo();
            entity.controller.ProcessInput(inputInfo);
            entity.controller.UpdateStateInfo(entity.stateInfo);

            entity.weapon.HandleTick();
            entity.weapon.UpdateStateInfo(entity.stateInfo);
        }

        // 复用 _broadcastList，避免每 tick 新建 List 与 LINQ ToList() 的 GC 开销
        _broadcastList.Clear();
        foreach (var entity in players.Values) _broadcastList.Add(entity.stateInfo);
        Broadcast(MessageType.AllPlayersInfo, _broadcastList);
    }

    /// <summary>
    /// 客户端连入：登记 endpoint，后续 Send 才能找到地址。
    /// 由客户端在 NetworkManager.Start 中 Send(MessageType.Connect, ...) 触发。
    /// </summary>
    private void OnConnect(string msg, IPEndPoint remote)
    {
        var connect = JsonConvert.DeserializeObject<PlayerConnect>(msg);
        if (connect == null || !players.TryGetValue(connect.uid, out var entity)) return;
        if (entity.endpoint == null)
        {
            entity.endpoint = new IPEndPoint(remote.Address, remote.Port);
            Debug.Log($"[NetworkManager] Player {connect.uid} endpoint registered: {entity.endpoint}");
        }
    }

    private void OnReady(string msg, IPEndPoint _)
    {
        var playerReady = JsonConvert.DeserializeObject<PlayerReady>(msg);
        if (players.TryGetValue(playerReady.uid, out var entity))
            entity.isReady = true;
    }

    private void OnInputInfo(string msg, IPEndPoint _)
    {
        var inputInfo = JsonConvert.DeserializeObject<PlayerInputInfo>(msg);
        if (players.TryGetValue(inputInfo.uid, out var entity))
        {
            if (entity.isDead) return;
            entity.controller.ApplyInput(inputInfo);
        }
    }

    private void OnFire(string msg, IPEndPoint _)
    {
        var playerFire = JsonConvert.DeserializeObject<PlayerFire>(msg);
        if (players.TryGetValue(playerFire.uid, out var entity))
        {
            if (entity.isDead) return;
            entity.weapon.Fire(playerFire.seed);
        }
    }

    private void OnReload(string msg, IPEndPoint _)
    {
        var playerReload = JsonConvert.DeserializeObject<PlayerReload>(msg);
        if (players.TryGetValue(playerReload.uid, out var entity))
        {
            if (!entity.isDead)
                entity.weapon.StartReload();
        }
        Broadcast(MessageType.Reload, playerReload);
    }

    private void OnSwitchWeapon(string msg, IPEndPoint _)
    {
        var playerSwitchWeapon = JsonConvert.DeserializeObject<PlayerSwitchWeapon>(msg);
        if (players.TryGetValue(playerSwitchWeapon.uid, out var entity))
            entity.weapon.SwitchWeapon(playerSwitchWeapon.index);
        Broadcast(MessageType.SwitchWeapon, playerSwitchWeapon);
    }

    private void OnPurchaseWeapon(string msg, IPEndPoint _)
    {
        if (MatchManager.instance.currentRoundState != RoundState.Preparation) return;
        var playerPurchaseWeapon = JsonConvert.DeserializeObject<PlayerPurchaseWeapon>(msg);
        if (!players.TryGetValue(playerPurchaseWeapon.uid, out var entity) || entity.state == null) return;

        WeaponConfig weaponConfig = WeaponDic.instance.weaponDic[playerPurchaseWeapon.id];
        if (entity.state.gold < weaponConfig.price) return;

        entity.state.gold -= weaponConfig.price;
        entity.weapon.AcquireWeapon(weaponConfig.id, weaponConfig.magazineCapacity, weaponConfig.magazineCapacity * 2);

        var playerAcquireWeapon = new PlayerAcquireWeapon(playerPurchaseWeapon.uid, weaponConfig.id);
        Broadcast(MessageType.AcquireWeapon, playerAcquireWeapon);
    }

    private void OnChat(string msg, IPEndPoint _)
    {
        var chat = JsonConvert.DeserializeObject<Chat>(msg);
        if (!players.TryGetValue(chat.uid, out var sender)) return;

        foreach (int uid in players.Keys)
        {
            if (!players.TryGetValue(uid, out var target)) continue;
            if (chat.area == ChatArea.All ||
                chat.area == ChatArea.Team && target.team == sender.team)
            {
                Send(uid, MessageType.Chat, chat);
            }
        }
    }

    private void OnPingPong(string msg, IPEndPoint _)
    {
        var pingPong = JsonConvert.DeserializeObject<PingPong>(msg);
        if (players.TryGetValue(pingPong.uid, out var entity) && entity.endpoint != null)
            Send(pingPong.uid, MessageType.PingPong, pingPong);
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

                // 收线程只负责拆包入队 + 拷贝 endpoint。endpoint 留给主线程
                // OnConnect handler 处理，避免在收线程里改业务状态。
                var endpointCopy = new IPEndPoint(remote.Address, remote.Port);
                messageList.Enqueue((type, str, endpointCopy));
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

    public static void Send<T>(int uid, MessageType type, T data)
    {
        if (!players.TryGetValue(uid, out var entity) || entity.endpoint == null) return;
        try
        {
            byte[] sendBuffer = PackMessage(type, data);
            udpServer.Send(sendBuffer, sendBuffer.Length, entity.endpoint);
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
        // 只序列化一次，所有玩家共享同一份字节缓冲
        byte[] sendBuffer;
        try { sendBuffer = PackMessage(type, data); }
        catch (Exception ex) { Debug.Log($"Broadcast pack error: {ex.Message}"); return; }

        foreach (var entity in players.Values)
        {
            if (entity.endpoint == null) continue;
            try { udpServer.Send(sendBuffer, sendBuffer.Length, entity.endpoint); }
            catch (SocketException ex) { Debug.Log($"UDP send error to {entity.uid}: {ex.Message}"); }
        }
    }

    /// <summary>
    /// 把 (type, data) 打包为 [length(4)][type(4)][json bytes] 的 UDP payload。
    /// 一次分配 sendBuffer，免掉中间多份 byte[] 与 BlockCopy 调用。
    /// </summary>
    private static byte[] PackMessage<T>(MessageType type, T data)
    {
        string dataStr = JsonConvert.SerializeObject(data);
        int dataLen = Encoding.UTF8.GetByteCount(dataStr);
        byte[] sendBuffer = new byte[8 + dataLen];
        BitConverter.TryWriteBytes(sendBuffer.AsSpan(0, 4), 4 + dataLen);
        BitConverter.TryWriteBytes(sendBuffer.AsSpan(4, 4), (int)type);
        Encoding.UTF8.GetBytes(dataStr, 0, dataStr.Length, sendBuffer, 8);
        return sendBuffer;
    }
}

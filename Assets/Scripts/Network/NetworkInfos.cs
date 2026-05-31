using UnityEngine;

public enum MessageType
{
    PingPong, JoinRoom, Connect, Ready, Start, GameProgress, 
    InputInfo, AllPlayersInfo, Fire, Reload, SwitchWeapon, PurchaseWeapon, AcquireWeapon, Kill, Hit, RoundEnd,
    Chat
}

public class PlayerConnect
{
    public int uid;
    public PlayerConnect(int uid)
    {
        this.uid = uid;
    }
}

public class PlayerStateInfo
{
    public int uid;
    public int slot, team;
    public int tick;
    public float positionX, positionY, positionZ;
    public float rotationX, rotationY;
    public float speed, velocity;
    public float height;
    public bool isCrouch;
    public int HP, armature, gold;
    public WeaponInfo[] weapons = new WeaponInfo[2];
    public int activeWeaponIndex;
    public PlayerStateInfo() { }
    public PlayerStateInfo(int uid, Vector3 position, float rotationY, float rotationX, float speed, float velocity, float height, bool isCrouch)
    {
        this.uid = uid;
        positionX = position.x;
        positionY = position.y;
        positionZ = position.z;
        this.rotationY = rotationY;
        this.rotationX = rotationX;
        this.speed = speed;
        this.velocity = velocity;
        this.height = height;
        this.isCrouch = isCrouch;
    }

    public PlayerStateInfo(PlayerStateInfo playerStateInfo)
    {
        uid = playerStateInfo.uid;
        positionX = playerStateInfo.positionX;
        positionY = playerStateInfo.positionY;
        positionZ = playerStateInfo.positionZ;
        rotationY = playerStateInfo.rotationY;
        rotationX = playerStateInfo.rotationX;
        speed = playerStateInfo.speed;
        velocity = playerStateInfo.velocity;
        height = playerStateInfo.height;
        isCrouch = playerStateInfo.isCrouch;
    }

    public Vector3 GetPosition()
    {
        return new Vector3(positionX, positionY, positionZ);
    }

    public void Initialize(int uid)
    {
        this.uid = uid;
        HP = 100;
        gold = MatchManager.INITIAL_GOLD;
        weapons[0] = new WeaponInfo(2, 12, 24);
    }
}

public class PlayerInputInfo
{
    public int uid;
    public int tick;
    public float moveInputX, moveInputY;
    public float lookInputX, lookInputY;
    public bool jump;
    public bool isWalk;
    public bool isCrouch;
    public PlayerInputInfo(int uid, float moveInputX, float moveInputY, float lookInputX, float lookInputY, bool jump, bool isWalk, bool isCrouch)
    {
        this.uid = uid;
        this.moveInputX = moveInputX;
        this.moveInputY = moveInputY;
        this.lookInputX = lookInputX;
        this.lookInputY = lookInputY;
        this.jump = jump;
        this.isWalk = isWalk;
        this.isCrouch = isCrouch;
    }
}

public class PlayerFire
{
    public int uid;
    public int seed;
    public float hitPointX, hitPointY, hitPointZ;
    public PlayerFire(int uid, int seed)
    {
        this.uid = uid;
        this.seed = seed;
    }
    public PlayerFire(int uid, Vector3 hitPoint)
    {
        this.uid = uid;
        hitPointX = hitPoint.x;
        hitPointY = hitPoint.y;
        hitPointZ = hitPoint.z;
    }
    public PlayerFire() { }
}

public class PlayerReload
{
    public int uid;
    public PlayerReload(int uid)
    {
        this.uid = uid;
    }
}

public class PlayerSwitchWeapon
{
    public int uid;
    public int index;
    public PlayerSwitchWeapon(int uid, int index)
    {
        this.uid = uid;
        this.index = index;
    }
}

public class PlayerPurchaseWeapon
{
    public int uid;
    public int id;
    public PlayerPurchaseWeapon(int uid, int id)
    {
        this.uid = uid;
        this.id = id;
    }
}

public class PlayerAcquireWeapon
{
    public int uid;
    public int id;
    public PlayerAcquireWeapon(int uid, int id)
    {
        this.uid = uid;
        this.id = id;
    }
}

public class PlayerKill
{
    public int killerUid;
    public int victimUid;
    public bool shotHead;
    public int weaponId;
    public PlayerKill(int killerUid, int victimUid, bool shotHead, int weaponId)
    {
        this.killerUid = killerUid;
        this.victimUid = victimUid;
        this.shotHead = shotHead;
        this.weaponId = weaponId;
    }
}

public class PlayerReady
{
    public int uid;
    public PlayerReady(int uid)
    {
        this.uid = uid;
    }
}

public class GameProgress
{
    public RoundState progress;
    public GameProgress(RoundState progress)
    {
        this.progress = progress;
    }
}

public class Hit
{
    public int uid;
    public float x, y, z;
    public Hit(int uid, Vector3 position)
    {
        this.uid = uid;
        x = position.x;
        y = position.y;
        z = position.z;
    }
    public Vector3 GetPosition()
    {
        return new Vector3(x, y, z);
    }
}

public class RoundEnd
{
    public int winTeam;
    public RoundEnd(int winTeam)
    {
        this.winTeam = winTeam;
    }
}

public enum ChatArea
{
    Team, All
}

public class Chat
{
    public int uid;
    public ChatArea area;
    public string text;
    public Chat(int uid, ChatArea area, string text)
    {
        this.uid = uid;
        this.area = area;
        this.text = text;
    }
}

public class PingPong
{
    public int uid;
    public int tick;
    public PingPong(int uid, int tick)
    {
        this.uid = uid;
        this.tick = tick;
    }
}

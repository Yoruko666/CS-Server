using UnityEngine;

public enum MessageType
{
    PingPong, JoinRoom, Connect, Ready, Start, GameProgress, 
    InputInfo, AllPlayersInfo, Fire, Reload, SwitchWeapon, PurchaseWeapon, AcquireWeapon, Kill, Hit, RoundEnd
}

public class PlayerConnect
{
    public string playerName;
    public PlayerConnect(string playerName)
    {
        this.playerName = playerName;
    }
}

public class PlayerStateInfo
{
    public string playerName;
    public int id, team;
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
    public PlayerStateInfo(string playerName, Vector3 position, float rotationY, float rotationX, float speed, float velocity, float height, bool isCrouch)
    {
        this.playerName = playerName;
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
        playerName = playerStateInfo.playerName;
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

    public void Initialize(string playerName)
    {
        this.playerName = playerName;
        HP = 100;
        gold = 10000;
        weapons[0] = new WeaponInfo(2, 12, 24);
    }
}

public class PlayerInputInfo
{
    public string playerName;
    public int tick;
    public float moveInputX, moveInputY;
    public float lookInputX, lookInputY;
    public bool jump;
    public bool isWalk;
    public bool isCrouch;
    public PlayerInputInfo(string playerName, float moveInputX, float moveInputY, float lookInputX, float lookInputY, bool jump, bool isWalk, bool isCrouch)
    {
        this.playerName = playerName;
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
    public string playerName;
    public int seed;
    public float hitPointX, hitPointY, hitPointZ;
    public PlayerFire(string playerName, int seed)
    {
        this.playerName = playerName;
        this.seed = seed;
    }
    public PlayerFire(string playerName, Vector3 hitPoint)
    {
        this.playerName = playerName;
        hitPointX = hitPoint.x;
        hitPointY = hitPoint.y;
        hitPointZ = hitPoint.z;
    }
    public PlayerFire() { }
}

public class PlayerReload
{
    public string playerName;
    public PlayerReload(string playerName)
    {
        this.playerName = playerName;
    }
}

public class PlayerSwitchWeapon
{
    public string playerName;
    public int index;
    public PlayerSwitchWeapon(string playerName, int index)
    {
        this.playerName = playerName;
        this.index = index;
    }
}

public class PlayerPurchaseWeapon
{
    public string playerName;
    public int id;
    public PlayerPurchaseWeapon(string playerName, int id)
    {
        this.playerName = playerName;
        this.id = id;
    }
}

public class PlayerAcquireWeapon
{
    public string playerName;
    public int id;
    public PlayerAcquireWeapon(string playerName, int id)
    {
        this.playerName = playerName;
        this.id = id;
    }
}

public class PlayerKill
{
    public string playerKillName;
    public string playerDieName;
    public bool shotHead;
    public int weaponId;
    public PlayerKill(string playerKillName, string playerDieName, bool shotHead, int weaponId)
    {
        this.playerKillName = playerKillName;
        this.playerDieName = playerDieName;
        this.shotHead = shotHead;
        this.weaponId = weaponId;
    }
}

public class PlayerReady
{
    public string playerName;
    public PlayerReady(string playerName)
    {
        this.playerName = playerName;
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
    public string playerName;
    public float x, y, z;
    public Hit(string playerName, Vector3 position)
    {
        this.playerName = playerName;
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

public class PingPong
{
    public string playerName;
    public int tick;
    public PingPong(string playerName, int tick)
    {
        this.playerName = playerName;
        this.tick = tick;
    }
}
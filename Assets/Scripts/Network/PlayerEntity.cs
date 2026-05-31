using System.Net;
using UnityEngine;

public class PlayerEntity
{
    public int uid;
    public int slot;
    public int team;
    public bool isReady;
    public bool isDead;

    public GameObject root;
    public IPEndPoint endpoint;
    public PlayerStateInfo stateInfo;

    public PlayerState state;
    public PlayerController controller;
    public WeaponManager weapon;

    public static PlayerEntity Create(GameObject go, int uid, int slot, int team)
    {
        return new PlayerEntity
        {
            uid = uid,
            slot = slot,
            team = team,
            root = go,
            stateInfo = new PlayerStateInfo(),
            state = go.GetComponent<PlayerState>(),
            controller = go.GetComponent<PlayerController>(),
            weapon = go.GetComponent<WeaponManager>(),
        };
    }
}

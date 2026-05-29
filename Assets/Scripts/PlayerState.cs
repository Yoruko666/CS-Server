using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [HideInInspector] public string playerName;
    [HideInInspector] public int id;
    [HideInInspector] public int HP, armature;
    [HideInInspector] public bool isDie;

    void Start()
    {
        HP = 100;
        armature = 0;
    }

    public void GetDamaged(string playerName, int damage, bool shotHead, int weaponId)
    {
        if (playerName == this.playerName)
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

            GameObject player = NetworkManager.playerPool[this.playerName];
            NetworkManager.playerDieList.Add(this.playerName);
            PlayerKill playerKill = new(playerName, this.playerName, shotHead, weaponId);
            NetworkManager.Broadcast(MessageType.Kill, playerKill);
            player.SetActive(false);
        }
    }

    public void Initialize(int id, string playerName)
    {
        this.id = id;
        this.playerName = playerName;
    }

    public void Reborn()
    {
        HP = 100; 
        armature = 0;
        isDie = false;
    }

    public void UpdateStateInfo(PlayerStateInfo state)
    {
        state.HP = HP;
        state.armature = armature;
    }
}

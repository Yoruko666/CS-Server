using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "SO/Weapon/WeaponConfig")]

public class WeaponConfig : ScriptableObject
{
    public int id;
    public int price;
    public GameObject weapon;
    public WeaponType weaponType;
    public string weaponName;
    public Sprite icon;
    public RuntimeAnimatorController characterAnimator;
    public int magazineCapacity;
    public int damage_head;
    public int damage_torso;
    public int damage_legs;
    public float shootSpeed;
    public float zoom;
    public float readyTime;
    public float fireTime;
    public float reloadTime;
    public bool isAuto;
    public bool isSniper;
    public bool hasAim;
    public bool hasAimFire;
    public bool hasBolt;
    public bool SingleReload;
    public AudioClip fireAudio;
    public AudioClip reloadAudio;
    public AudioClip boltAudio;
    public AudioClip reloadOpenAudio;
    public AudioClip reloadInsertAudio;
    public AudioClip reloadCloseAudio;
}

public enum WeaponType
{
    MainGun, Handgun
}
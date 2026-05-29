using System.Collections.Generic;
using UnityEngine;

public class WeaponDic : MonoBehaviour
{
    public WeaponDatabase weaponDatabase;
    [HideInInspector] public List<WeaponConfig> weaponDic = new List<WeaponConfig>();

    public static WeaponDic instance;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        weaponDic = weaponDatabase.weaponDatabase;
    }
}

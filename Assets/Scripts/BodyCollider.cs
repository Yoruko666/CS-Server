using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyCollider : MonoBehaviour
{
    public Transform character;
    public BodyPart part;

    public void GetDamaged(string playerName, int damage, int weaponId)
    {
        character.GetComponent<PlayerState>().GetDamaged(playerName, damage, part == BodyPart.Head, weaponId);
    }
}

public enum BodyPart
{
    Head, Torso, Legs
}
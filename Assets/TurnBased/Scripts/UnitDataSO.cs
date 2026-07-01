using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Unit_Fulan", menuName = "Units/UnitData")]
public class UnitDataSO : ScriptableObject
{
    public new string name;
    public Sprite sprite;

    public UnitStats stats;
    public List<CombatActionSO> combatActionList;
}

[Serializable]
public class UnitStats
{
    public float maxHealth = 100;
    public float currentHealth = 100;

    public float combatAttack = 1;
    public float combatDefense = 0;
    public float combatSpeed = 10;

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }
}

[Serializable]
public enum UnitFactionEnum { Player, Enemies }

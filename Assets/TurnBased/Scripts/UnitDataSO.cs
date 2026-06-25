using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Unit_Fulan", menuName = "Units/UnitData")]
public class UnitDataSO : ScriptableObject
{
    public new string name;

    public UnitStats stats;
}

[Serializable]
public class UnitStats
{
    public float maxHealth;
    public float currentHealth;

    public float combatAttack;
    public float combatDefense;
    public float combatSpeedInit;
    public float combatSpeedBank;
    public int combatEnergy;
}

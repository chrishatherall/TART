using UnityEngine;
using System.Collections;

namespace TART
{
    interface IDamageTaker
    {
        public void TakeDamage(int dmg, Vector3 hitDirection, int sourcePlayerID);
    }

    // Events for STUFF happening. This should contain loads of things, so we can easily make weird weapons and items.
    public enum Events
    {
        AutoSomething,
        Preround,     // Preround started
        RoundStart,   // A round began
        Postround,    // A round ended, postround began
        InnocentWin,
        TraitorWin,
        PlayerDied    // A player died during a round
    }

    public class TartRole
    {
        public int ID;
        public string Name;
        public Color ClassColour;

        public TartRole (int id, string name, Color colour)
        {
            ID = id;
            Name = name;
            ClassColour = colour;
        }
    }

    // Used to define an amount of damage, including its source player
    public class Damage
    {
        public int SourcePlayerID;
        public int Amount;

        public Damage(int sourcePlayerID, int amount)
        {
            SourcePlayerID = sourcePlayerID;
            Amount = amount;
        }

        public Damage(string sourcePlayerID, string amount)
        {
            SourcePlayerID = int.Parse(sourcePlayerID);
            Amount = int.Parse(amount);
        }
    }

}

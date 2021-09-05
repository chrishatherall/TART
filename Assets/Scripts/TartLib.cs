using UnityEngine;
using System.Collections;

namespace TART
{
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

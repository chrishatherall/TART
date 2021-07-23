using UnityEngine;
using System.Collections;

namespace tart
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

}

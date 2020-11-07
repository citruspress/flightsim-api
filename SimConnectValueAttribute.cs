using System;

namespace FlightSimApi
{
    public class SimConnectValueAttribute : Attribute
    {
        public SimConnectValueAttribute(string stringName, string unitName = null)
        {
            StringName = stringName;
            UnitName = unitName;
        }

        public string StringName { get; }
        public string UnitName { get; }
    }
}

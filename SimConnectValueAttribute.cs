using System;

namespace FlightSimApi
{
    public class SimConnectValueAttribute : Attribute
    {
        public SimConnectValueAttribute(string stringName)
        {
            StringName = stringName;
        }

        public string StringName { get; }
    }
}

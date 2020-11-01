using System;

namespace FlightSimApi
{
    public class SimConnectEventAttribute : Attribute
    {
        public SimConnectEventAttribute(string eventName)
        {
            EventName = eventName;
        }

        public string EventName { get; }
    }
}

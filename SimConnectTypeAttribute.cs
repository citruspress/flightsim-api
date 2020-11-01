using System;

namespace FlightSimApi
{
    public class SimConnectTypeAttribute : Attribute
    {
        public SimConnectTypeAttribute(object typeDefinition, int samplingFrequency)
        {
            TypeDefinition = typeDefinition as Enum;
            SamplingFrequency = samplingFrequency;
        }

        public Enum TypeDefinition { get; }
        public int SamplingFrequency { get; }
    }
}

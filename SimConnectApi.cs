using System;
using Microsoft.FlightSimulator.SimConnect;
using System.Reflection;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FlightSimApi
{
    public class SimConnectApi
    {
        enum Priority : uint
        {
            SIMCONNECT_GROUP_PRIORITY_HIGHEST_MASKABLE = 10000000, // The highest priority that allows events to be masked.
            SIMCONNECT_GROUP_PRIORITY_STANDARD = 1900000000, // The standard priority.
            SIMCONNECT_GROUP_PRIORITY_DEFAULT = 2000000000, // The default priority.
            SIMCONNECT_GROUP_PRIORITY_LOWEST = 4000000000 // Priorities lower than this will be ignored.
        };

        private SimConnect _sim;
        private readonly ConcurrentDictionary<Type, ValueType> _values = new ConcurrentDictionary<Type, ValueType>();
        private RegisteredType[] _registeredTypes = new RegisteredType[0];
        private List<Action<SimConnect>> _registeredEvents = new List<Action<SimConnect>>();
        private readonly Task _connectTask;
        private bool _disposed;

        public bool IsConnected => _sim != null;

        public SimConnectApi()
        {
            _connectTask = new Task(async () =>
            {
                while (!_disposed)
                {
                    try
                    {
                        Connect();
                        while (_sim != null)
                        {
                            RequestData();
                            _sim.ReceiveMessage();
                            await Task.Delay(1);
                        }
                    }
                    catch (Exception)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            });
        }

        public void Start() => _connectTask.Start();

        public void RegisterType<T>() where T : struct
        {
            var structType = typeof(T);
            var simConnectTypeAttribute = structType.GetCustomAttribute<SimConnectTypeAttribute>();
            if (simConnectTypeAttribute == null)
                throw new ArgumentException("Type has to have the SimConnectTypeAttribute");

            _registeredTypes = _registeredTypes.Concat(new[] {
                new RegisteredType
                {
                    SamplingFrequency = TimeSpan.FromMilliseconds(simConnectTypeAttribute.SamplingFrequency),
                    Type = simConnectTypeAttribute.TypeDefinition,
                    RegisterType = (simConnect) =>
                    {
                        foreach (var field in structType.GetFields())
                        {
                            var valueAttribute = field.GetCustomAttribute<SimConnectValueAttribute>();
                            if (valueAttribute == null)
                                continue;

                            simConnect.AddToDataDefinition(simConnectTypeAttribute.TypeDefinition, valueAttribute.StringName, null, SIMCONNECT_DATATYPE.STRING256, 0, SimConnect.SIMCONNECT_UNUSED);
                        }

                        simConnect.RegisterDataDefineStruct<T>(simConnectTypeAttribute.TypeDefinition);
                    }
                }
            }).ToArray();
        }

        public void RegisterEvents<T>() where T : Enum
        {
            var enumType = typeof(T);

            _registeredEvents.Add((simConnect) =>
            {
                foreach (var member in enumType.GetMembers(BindingFlags.Public | BindingFlags.Static))
                {
                    var simConnectEventAttribute = member.GetCustomAttribute<SimConnectEventAttribute>();
                    if (simConnectEventAttribute == null)
                        throw new ArgumentException("Enum has to have the SimConnectEventAttribute");

                    simConnect.MapClientEventToSimEvent((T)Enum.Parse(enumType, member.Name), simConnectEventAttribute.EventName);
                }
            });
        }

        public T Get<T>() where T : struct
        {
            if (_values.TryGetValue(typeof(T), out var value))
            {
                return (T)value;
            }

            return default(T);
        }

        public void SendEvent(Enum eventId, uint data = 0)
        {
            _sim?.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, eventId, data, Priority.SIMCONNECT_GROUP_PRIORITY_STANDARD, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }

        private void Connect()
        {
            try
            {
                _sim = new SimConnect("SimConnectNetCoreApi", IntPtr.Zero, 0, null, 0);
                _sim.OnRecvQuit += Sim_OnRecvQuit;
                _sim.OnRecvSimobjectDataBytype += Sim_OnRecvSimobjectDataBytype;
                _registeredEvents.ForEach(e => e(_sim));
                foreach (ref readonly var t in _registeredTypes.AsSpan())
                {
                    t.RegisterType(_sim);
                }
            }
            catch
            {
                _sim = null;
                throw;
            }
        }

        private void RequestData()
        {
            foreach (ref readonly var registeredType in _registeredTypes.AsSpan())
            {
                if (!registeredType.ShouldRequestData)
                    continue;

                registeredType.RequestData(_sim);
            }
        }

        private void Sim_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            _values[data.dwData[0].GetType()] = (ValueType)data.dwData[0];
        }

        private void Sim_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            _sim = null;
        }

        public void Dispose()
        {
            _disposed = true;
            _sim?.Dispose();
            _sim = null;
        }

        private struct RegisteredType
        {
            public Enum Type;
            public TimeSpan SamplingFrequency;
            public Action<SimConnect> RegisterType;
            private DateTime _nextSampleTime;

            public bool ShouldRequestData => DateTime.UtcNow >= _nextSampleTime;

            public void RequestData(SimConnect sim)
            {
                sim.RequestDataOnSimObjectType(Type, Type, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                _nextSampleTime = DateTime.UtcNow.Add(SamplingFrequency);
            }
        }
    }
}

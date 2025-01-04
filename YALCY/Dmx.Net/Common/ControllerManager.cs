using System;
using System.Collections.Generic;
using System.Linq;
using Dmx.Net.Attributes;
using System.Reflection;

namespace Dmx.Net.Common
{
    public static class ControllerManager
    {
        public static Dictionary<int, IController> RegisteredControllers { get; }
        public static IEnumerable<ControllerInfo> SupportedControllers { get; }

        static ControllerManager()
        {
            RegisteredControllers = new();
            SupportedControllers = Assembly.GetExecutingAssembly()
                .GetExportedTypes()
                .Where(x =>
                    x.BaseType == typeof(ControllerBase) &&
                    x.IsDefined(typeof(ControllerAttribute)) &&
                    !x.IsDefined(typeof(ExperimentalAttribute))
                ).Select(x => new ControllerInfo(x));
        }

        /// <summary>
        /// Returns all detected devices supported by the manager.
        /// </summary>
        public static IEnumerable<Device> GetDevices()
        {
            foreach (var type in SupportedControllers)
            {
                var method = type.Type.GetMethod("GetDevices");
                var result = method?.Invoke(null, null) as IEnumerable<Device>;
                if (result is not null)
                {
                    foreach (var device in result)
                    {
                        yield return device;
                    };
                }
            }
        }

        /// <summary>
        /// Registers a controller in the universe.
        /// </summary>
        /// <typeparam name="T">Controller type</typeparam>
        /// <param name="universe">Universe number</param>
        /// <param name="timer">Timer instance</param>
        /// <exception cref="ArgumentException"></exception>
        public static T RegisterController<T>(int universe, DmxTimer timer) where T : IController
        {
            if (RegisteredControllers.ContainsKey(universe))
            {
                throw new ArgumentException("The universe already has a controller registered");
            }

            var type = SupportedControllers.Where(x => x.Type == typeof(T)).FirstOrDefault();

            if (type is null)
            {
                throw new ArgumentException("Provided controller type is not supported.");
            }

            var instance = (T)Activator.CreateInstance(typeof(T), timer)!;
            RegisteredControllers.Add(universe, (IController)instance);

            return instance;
        }

        /// <summary>
        /// Returns a controller registered in the universe.
        /// </summary>
        /// <param name="universe">Universe number</param>
        /// <exception cref="ArgumentException"></exception>
        public static IController GetController(int universe)
        {
            if (!RegisteredControllers.TryGetValue(universe, out var instance))
            {
                throw new ArgumentException("The universe has no registered controllers.");
            }

            return instance;
        }

        /// <summary>
        /// Unregisters a controller at the universe.
        /// </summary>
        /// <param name="universe">Universe number</param>
        /// <exception cref="ArgumentException"></exception>
        public static void UnregisterController(int universe)
        {
            if (!RegisteredControllers.TryGetValue(universe, out var instance))
            {
                throw new ArgumentException("The universe has no registered controllers.");
            }

            RegisteredControllers.Remove(universe);
            instance.Dispose();
        }

        /// <summary>
        /// Unregisters and disposes all registered controllers.
        /// </summary>
        public static void UnregisterAll()
        {
            foreach (var controller in RegisteredControllers.Values)
            {
                controller.Dispose();
            }

            RegisteredControllers.Clear();
        }
    }
}

﻿namespace Eco.Mods.SmartTax
{
    using Shared.Logging;
    using Shared.Localization;

    public static class Logger
    {
        public static void Debug(string message) => Log.Write(new LocString("[SmartTax] DEBUG: " + message + "\n"));

        public static void Info(string message) => Log.Write(new LocString("[SmartTax] " + message + "\n"));

        public static void Error(string message) => Log.Write(new LocString("[SmartTax] ERROR: " + message + "\n"));
    }
}
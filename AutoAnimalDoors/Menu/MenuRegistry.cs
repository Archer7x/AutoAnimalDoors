using AutoAnimalDoors.Config;
using AutoAnimalDoors.StardewValleyWrapper;
using GenericModConfigMenu;
using StardewModdingAPI;
using System;
using System.Collections.Generic;

namespace AutoAnimalDoors.Menu
{
    public class MenuRegistry
    {
        private static readonly int MIN_TIMEPICKER_HOUR = 6; // 6am when you get up
        private static readonly int MAX_TIMEPICKER_HOUR = 26; // 2am the next day when you pass out
        private static readonly int HOUR_DIVISOR = 100;
        private static readonly int MINUTE_INCREMENT = 10;
        private static readonly int MIN_TIMEPICKER_VALUE = 0;

        private static int MINUTE_INCREMENTS_PER_HOUR
        {
            get { return 60 / MINUTE_INCREMENT; }
        }
        private static int MAX_TIMEPICKER_VALUE
        {
            get { return MIN_TIMEPICKER_VALUE + (MAX_TIMEPICKER_HOUR - MIN_TIMEPICKER_HOUR) * MINUTE_INCREMENTS_PER_HOUR; }
        }

        public event EventHandler<bool> AutoOpenedEnabledChanged;

        private readonly SortedList<int, String> animalBuildingLevelOptions = new()
        {
            { 1, "Normal" },
            { 2, "Groß" },
            { 3, "Deluxe" },
            { int.MaxValue, "Deaktiviert" }
        };

        private string[] AnimalBuildingLevelNames
        {
            get
            {
                string[] names = new string[animalBuildingLevelOptions.Count];
                animalBuildingLevelOptions.Values.CopyTo(names, 0);
                return names;
            }
        }

        private IModHelper Helper { get; set; }

        public MenuRegistry(IModHelper helper)
        {
            Helper = helper;
        }

        public void InitializeMenu(IManifest manifest, ModConfig config)
        {
            IGenericModConfigMenuApi api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api != null)
            {
                Logger.Instance.Log("Generic Mod Config detected, initializing menu");

                api.Register(manifest, () => config = new ModConfig(), () => Helper.WriteConfig<ModConfig>(config));

                api.AddSectionTitle(manifest, () => "Automatische Stalltüren");
                api.AddBoolOption(mod: manifest,
                    name: () => "Automatisches Öffnen Aktiviert",
                    getValue: () => config.AutoOpenEnabled,
                    setValue: (bool autoOpenEnabled) =>
                    {
                        config.AutoOpenEnabled = autoOpenEnabled;
                        AutoOpenedEnabledChanged?.Invoke(this, autoOpenEnabled);
                    });

                api.AddNumberOption(mod: manifest,
                    name: () =>
                    {
                        string alreadyOpened = ModEntry.HasDoorsOpenedToday ? "(Heute bereits geöffnet)" : "";
                        return "Öffnungszeit " + alreadyOpened;
                    },
                    tooltip: () => "Der Zeitpunkt, zu dem die Türen der Tiere geöffnet werden sollen.",
                    getValue: () => GetTimePickerValueFromTime(config.AnimalDoorOpenTime),
                    setValue: (int newTime) => config.AnimalDoorOpenTime = GetTimeFromTimePickerValue(newTime),
                    min: MIN_TIMEPICKER_VALUE,
                    max: MAX_TIMEPICKER_VALUE,
                    formatValue: (value) => GetTimePickerStringFromTimePickerValue(value));


                api.AddNumberOption(mod: manifest,
                    name: () =>
                    {
                        string alreadyClosed = ModEntry.HasDoorsClosedToday ? "(Heute bereits geschlossen)" : "";
                        return "Schließzeit " + alreadyClosed;
                    },
                    tooltip: () => "Der Zeitpunkt, zu dem die Stalltüren geschlossen werden sollen.",
                    getValue: () => GetTimePickerValueFromTime(config.AnimalDoorCloseTime),
                    setValue: (int newTime) => config.AnimalDoorCloseTime = GetTimeFromTimePickerValue(newTime),
                    min: MIN_TIMEPICKER_VALUE,
                    max: MAX_TIMEPICKER_VALUE,
                    formatValue: (value) => GetTimePickerStringFromTimePickerValue(value));

                api.AddBoolOption(mod: manifest,
                    name: () => "Andere Mods Aktiviert",
                    tooltip: () => "Aktiviert oder deaktiviert das automatische Öffnen von Stalltüren durch andere Mods (Ich kann nicht die Türgeräusche/Animation kontrollieren oder jeden Mod testen).",
                    getValue: () => config.UnrecognizedAnimalBuildingsEnabled,
                    setValue: (bool unrecognizedAnimalBulidingsEnabled) => config.UnrecognizedAnimalBuildingsEnabled = unrecognizedAnimalBulidingsEnabled);

                api.AddTextOption(mod: manifest,
                    name: () => "Koop Erforderliche Ausbaustufe",
                    tooltip: () => "Die für das automatische Öffnen/Schließen erforderliche Upgrade-Stufe des Stalles.",
                    getValue: () => GetAnimalBuildingUpgradeLevelName(config.CoopRequiredUpgradeLevel),
                    setValue: (string newLevel) => config.CoopRequiredUpgradeLevel = GetAnimalBuildingUpgradeLevel(newLevel),
                    allowedValues: AnimalBuildingLevelNames);

                api.AddTextOption(mod: manifest,
                    name: () => "Erforderliche Ausbaustufe der Scheune",
                    tooltip: () => "The barn upgrade level required for auto open/close.",
                    getValue: () => GetAnimalBuildingUpgradeLevelName(config.BarnRequiredUpgradeLevel),
                    setValue: (string newLevel) => config.BarnRequiredUpgradeLevel = GetAnimalBuildingUpgradeLevel(newLevel),
                    allowedValues: AnimalBuildingLevelNames);

                api.AddTextOption(mod: manifest,
                    name: () => "Türgeräusch Einstellung",
                    tooltip: () => "Wann das Türgeräusch beim Öffnen und Schließen von Türen abgespielt werden soll bzw. nicht abgespielt werden soll.",
                    getValue: () => config.DoorSoundSetting.Name(),
                    setValue: (string doorSoundSettingName) => config.DoorSoundSetting = DoorSoundSettingUtils.FromName(doorSoundSettingName),
                    allowedValues: DoorSoundSettingUtils.Names);
                
                api.AddBoolOption(mod: manifest,
                    name: () => "Meldung anzeigen",
                    tooltip: () => "Wenn aktiviert, wird eine Meldung angezeigt, wenn alle Türen geschlossen/geöffnet wurden.",
                    getValue: () => config.DoorEventPopupEnabled,
                    setValue: (bool doorClosePopupEnabled) => config.DoorEventPopupEnabled = doorClosePopupEnabled);

                api.AddBoolOption(mod: manifest,
                    name: () => "Alle gleichzeitig schließen",
                    tooltip: () => "Wenn diese Option aktiviert ist, werden alle Türen gleichzeitig geschlossen, sobald alle Tiere im Stall sind. Andernfalls werden sie geschlossen, wenn alle Tiere eines einzelnen Stalles drinnen sind.",
                    getValue: () => config.CloseAllBuildingsAtOnce,
                    setValue: (bool closeAllAtOnce) => config.CloseAllBuildingsAtOnce = closeAllAtOnce);

                api.AddBoolOption(mod: manifest,
                    name: () => "Türen bei Regen öffnen",
                    tooltip: () => "Aktiviert oder deaktiviert das Öffnen der Türen bei Regen/Blitz.",
                    getValue: () => config.OpenDoorsWhenRaining,
                    setValue: (bool autoOpenEnabled) => config.OpenDoorsWhenRaining = autoOpenEnabled);

                api.AddBoolOption(mod: manifest,
                    name: () => "Türen im Winter öffnen",
                    tooltip: () => "Aktiviert oder deaktiviert das Öffnen von Türen im Winter.",
                    getValue: () => config.OpenDoorsDuringWinter,
                    setValue: (bool autoOpenEnabled) => config.OpenDoorsDuringWinter = autoOpenEnabled);
            }
        }

        private static int GetTimePickerValueFromTime(int time)
        {
            int hour = (time / HOUR_DIVISOR) - MIN_TIMEPICKER_HOUR;
            int min = (time % HOUR_DIVISOR) / MINUTE_INCREMENT;
            return hour * MINUTE_INCREMENTS_PER_HOUR + min;
        }

        private static int GetTimeFromTimePickerValue(int timePickerValue)
        {
            int hour = (timePickerValue / MINUTE_INCREMENTS_PER_HOUR) + MIN_TIMEPICKER_HOUR;
            int min = timePickerValue % MINUTE_INCREMENTS_PER_HOUR;
            return hour * HOUR_DIVISOR + min * MINUTE_INCREMENT;
        }

        private string GetAnimalBuildingUpgradeLevelName(int level)
        {
            if (animalBuildingLevelOptions.TryGetValue(level, out string upgradeLevelName))
            {
                return upgradeLevelName;
            }

            return animalBuildingLevelOptions.Values[0];
        }

        private int GetAnimalBuildingUpgradeLevel(string name)
        {
            int index = animalBuildingLevelOptions.IndexOfValue(name);
            if (index < 0)
            {
                index = 0;
            }
            return animalBuildingLevelOptions.Keys[index];
        }

        private string GetTimePickerStringFromTimePickerValue(int timePickerValue)
        {
            int time = GetTimeFromTimePickerValue(timePickerValue);
            int hour = time / HOUR_DIVISOR;
            int min = time % HOUR_DIVISOR;
            bool isAM = hour % 24 < 12;
            int hourLabel = hour % 12;
            if (hourLabel == 0)
            {
                hourLabel = 12;
            }
            return string.Format("{0}:{1:D2} {2}", hourLabel, min, isAM ? "AM" : "PM");
        }
    }
}

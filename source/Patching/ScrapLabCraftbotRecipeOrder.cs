using System;
using System.Collections.Generic;
using System.IO;

namespace RaidRescue
{
    // Keeps every Craftbot recipe owned by a ScrapLab custom part together,
    // immediately after Scrap Mechanic's ordinary Vacuum Pipe recipe.  Each
    // patch service still owns and removes only its own entry; this coordinator
    // merely gives the shared JSON array one deterministic order.
    internal static class ScrapLabCraftbotRecipeOrder
    {
        internal const string VanillaVacuumPipeUuid =
            "9b8f2abd-265c-4750-b8b9-fe6cb564633c";
        internal const string WirelessVacuumPipeUuid =
            "a34d9af0-4ba0-431d-b647-2d5435ecf138";
        internal const string NetworkStorageChestUuid =
            "bc7576a7-f226-459a-883c-e8460e955d63";

        internal static string WirelessVacuumPipeRecipe
        {
            get
            {
                return "\t{\n" +
                    "\t\t\"itemId\": \"" + WirelessVacuumPipeUuid + "\",\n" +
                    "\t\t\"quantity\": 2,\n" +
                    "\t\t\"craftTime\": 30,\n" +
                    "\t\t\"ingredientList\": [\n" +
                    "\t\t\t{\n" +
                    "\t\t\t\t\"quantity\": 2,\n" +
                    "\t\t\t\t\"itemId\": \"" + VanillaVacuumPipeUuid + "\"\n" +
                    "\t\t\t},\n" +
                    "\t\t\t{\n" +
                    "\t\t\t\t\"quantity\": 2,\n" +
                    "\t\t\t\t\"itemId\": \"5530e6a0-4748-4926-b134-50ca9ecb9dcf\"\n" +
                    "\t\t\t},\n" +
                    "\t\t\t{\n" +
                    "\t\t\t\t\"quantity\": 4,\n" +
                    "\t\t\t\t\"itemId\": \"f152e4df-bc40-44fb-8d20-3b3ff70cdfe3\"\n" +
                    "\t\t\t}\n" +
                    "\t\t]\n" +
                    "\t}";
            }
        }

        internal static string NetworkStorageChestRecipe
        {
            get
            {
                return "\t{\n\t\t\"itemId\": \"" + NetworkStorageChestUuid +
                    "\",\n\t\t\"quantity\": 1,\n\t\t\"craftTime\": 30,\n" +
                    "\t\t\"ingredientList\": [\n" +
                    "\t\t\t{ \"quantity\": 1, \"itemId\": \"4c474cff-3f6a-4306-93d1-c4c74578afd2\" },\n" +
                    "\t\t\t{ \"quantity\": 10, \"itemId\": \"5530e6a0-4748-4926-b134-50ca9ecb9dcf\" },\n" +
                    "\t\t\t{ \"quantity\": 20, \"itemId\": \"f152e4df-bc40-44fb-8d20-3b3ff70cdfe3\" }\n" +
                    "\t\t]\n\t}";
            }
        }

        internal static string PlaceRecipe(string text, string partUuid)
        {
            if (String.IsNullOrEmpty(text))
                throw new InvalidDataException("craftbot_core.json is empty.");
            if (partUuid != WirelessVacuumPipeUuid &&
                partUuid != NetworkStorageChestUuid)
                throw new InvalidDataException("The ScrapLab Craftbot recipe owner is unknown.");
            if (AdaptivePatchSupport.Count(text, partUuid) != 0)
                throw new InvalidDataException("The ScrapLab Craftbot recipe already exists or conflicts.");

            string working = text;
            bool includeWireless = partUuid == WirelessVacuumPipeUuid;
            bool includeStorage = partUuid == NetworkStorageChestUuid;

            if (partUuid != WirelessVacuumPipeUuid &&
                AdaptivePatchSupport.Count(working, WirelessVacuumPipeRecipe) == 1)
            {
                working = RemoveExactRecipe(working, WirelessVacuumPipeRecipe);
                includeWireless = true;
            }
            if (partUuid != NetworkStorageChestUuid &&
                AdaptivePatchSupport.Count(working, NetworkStorageChestRecipe) == 1)
            {
                working = RemoveExactRecipe(working, NetworkStorageChestRecipe);
                includeStorage = true;
            }

            List<string> group = new List<string>();
            if (includeWireless) group.Add(WirelessVacuumPipeRecipe);
            if (includeStorage) group.Add(NetworkStorageChestRecipe);
            int anchorEnd = FindRecipeObjectEnd(working, VanillaVacuumPipeUuid);
            int next = anchorEnd + 1;
            while (next < working.Length && Char.IsWhiteSpace(working[next])) next++;
            if (next >= working.Length || working[next] != ',')
                throw new InvalidDataException("The vanilla Vacuum Pipe recipe position changed.");
            return working.Insert(anchorEnd + 1, ",\n" + String.Join(",\n", group.ToArray()));
        }

        internal static string RemoveRecipe(string text, string partUuid)
        {
            string recipe = partUuid == WirelessVacuumPipeUuid
                ? WirelessVacuumPipeRecipe
                : partUuid == NetworkStorageChestUuid
                    ? NetworkStorageChestRecipe
                    : null;
            if (recipe == null)
                throw new InvalidDataException("The ScrapLab Craftbot recipe owner is unknown.");
            return RemoveExactRecipe(text, recipe);
        }

        private static string RemoveExactRecipe(string text, string recipe)
        {
            string value = ",\n" + recipe;
            int count = AdaptivePatchSupport.Count(text, value);
            if (count != 1)
                throw new InvalidDataException("A protected ScrapLab Craftbot recipe changed or appears " + count + " times.");
            return text.Replace(value, "");
        }

        private static int FindRecipeObjectEnd(string text, string uuid)
        {
            string marker = "\"itemId\": \"" + uuid + "\"";
            int markerCount = AdaptivePatchSupport.Count(text, marker);
            if (markerCount != 1)
                throw new InvalidDataException("The vanilla Vacuum Pipe recipe marker changed or appears " + markerCount + " times.");
            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            int start = markerIndex;
            while (start >= 0 && text[start] != '{') start--;
            if (start < 0)
                throw new InvalidDataException("The vanilla Vacuum Pipe recipe object start is missing.");

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int index = start; index < text.Length; index++)
            {
                char character = text[index];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"') { inString = true; continue; }
                if (character == '{') depth++;
                else if (character == '}' && --depth == 0) return index;
            }
            throw new InvalidDataException("The vanilla Vacuum Pipe recipe object ending is missing.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Xml;

namespace RaidRescue
{
    internal sealed class ItemCatalogEntry
    {
        public string Name;
        public string Description;
        public string IconDataUrl;
        public int RecoveryValue;
        public string RecoveryTier;
    }

    internal sealed class ItemRecipeIngredient
    {
        public string Uuid;
        public int Quantity;
    }

    internal sealed class ItemRecipe
    {
        public string Uuid;
        public int Quantity;
        public int CraftTime;
        public List<ItemRecipeIngredient> Ingredients;
    }

    internal sealed class ItemIconLocation
    {
        public string AtlasPath;
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    internal static class ItemCatalog
    {
        private static readonly object Sync = new object();
        private static bool loaded;
        private static readonly Dictionary<string, ItemCatalogEntry> Items =
            new Dictionary<string, ItemCatalogEntry>(
                StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ItemIconLocation> Icons =
            new Dictionary<string, ItemIconLocation>(
                StringComparer.OrdinalIgnoreCase);

        public static ItemCatalogEntry Find(string uuid)
        {
            EnsureLoaded();
            lock (Sync)
            {
                ItemCatalogEntry entry;
                if (!Items.TryGetValue(uuid ?? String.Empty, out entry))
                {
                    entry = new ItemCatalogEntry
                    {
                        Name = "Unknown item",
                        Description = "Scrap Mechanic did not provide a local name for this item."
                    };
                    Items[uuid ?? String.Empty] = entry;
                }

                if (entry.IconDataUrl == null)
                    entry.IconDataUrl = ReadIcon(uuid);

                return new ItemCatalogEntry
                {
                    Name = entry.Name,
                    Description = entry.Description,
                    IconDataUrl = entry.IconDataUrl,
                    RecoveryValue = entry.RecoveryValue,
                    RecoveryTier = entry.RecoveryTier
                };
            }
        }

        private static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (loaded)
                    return;
                loaded = true;

                string gamePath = GameInstallLocator.Find();
                if (String.IsNullOrEmpty(gamePath))
                    return;

                ReadDescriptions(Path.Combine(
                    gamePath, "Data", "Gui", "Language", "English",
                    "InventoryItemDescriptions.json"));
                ReadDescriptions(Path.Combine(
                    gamePath, "Survival", "Gui", "Language", "English",
                    "inventoryDescriptions.json"));
                AssignRecoveryValues(gamePath);

                ReadIconMap(Path.Combine(
                    gamePath, "Data", "Gui", "IconMap.xml"));
                ReadIconMap(Path.Combine(
                    gamePath, "Survival", "Gui", "IconMapSurvival.xml"));
                ReadIconMap(Path.Combine(
                    gamePath, "Data", "Gui", "ToolIconMap.xml"));
            }
        }

        private static void AssignRecoveryValues(string gamePath)
        {
            Dictionary<string, int> baseValues =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ItemCatalogEntry> pair in Items)
                baseValues[pair.Key] = BaselineRecoveryValue(
                    pair.Key, pair.Value == null ? null : pair.Value.Name);

            Dictionary<string, int> recipeValues =
                CalculateRecipeValues(
                    ReadRecipes(Path.Combine(
                        gamePath, "Survival", "CraftingRecipes")),
                    baseValues);

            foreach (KeyValuePair<string, ItemCatalogEntry> pair in Items)
            {
                int value;
                if (!baseValues.TryGetValue(pair.Key, out value))
                    value = 40;
                int craftedValue;
                if (recipeValues.TryGetValue(pair.Key, out craftedValue))
                    value = Math.Max(value, craftedValue);
                pair.Value.RecoveryValue = value;
                pair.Value.RecoveryTier = RecoveryTier(value);
            }
        }

        private static List<ItemRecipe> ReadRecipes(string directory)
        {
            List<ItemRecipe> recipes = new List<ItemRecipe>();
            if (!Directory.Exists(directory))
                return recipes;
            try
            {
                string[] paths = Directory.GetFiles(
                    directory, "*.json", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                foreach (string path in paths)
                    ReadRecipeFile(path, recipes);
            }
            catch
            {
                // Name-based values still provide a complete stable order.
            }
            return recipes;
        }

        private static void ReadRecipeFile(
            string path, List<ItemRecipe> recipes)
        {
            try
            {
                string json = File.ReadAllText(path);
                int arrayStart = json.IndexOf('[');
                if (arrayStart < 0)
                    return;
                object[] entries = new JavaScriptSerializer
                {
                    MaxJsonLength = Int32.MaxValue
                }.DeserializeObject(json.Substring(arrayStart)) as object[];
                if (entries == null)
                    return;

                foreach (object rawEntry in entries)
                {
                    Dictionary<string, object> entry =
                        rawEntry as Dictionary<string, object>;
                    if (entry == null)
                        continue;
                    string output = DictionaryString(entry, "itemId");
                    int outputQuantity = DictionaryInt(
                        entry, "quantity", 1);
                    object ingredientValue;
                    object[] rawIngredients =
                        entry.TryGetValue(
                            "ingredientList", out ingredientValue)
                            ? ingredientValue as object[]
                            : null;
                    if (String.IsNullOrWhiteSpace(output) ||
                        outputQuantity <= 0 ||
                        rawIngredients == null ||
                        rawIngredients.Length == 0)
                        continue;

                    List<ItemRecipeIngredient> ingredients =
                        new List<ItemRecipeIngredient>();
                    foreach (object rawIngredient in rawIngredients)
                    {
                        Dictionary<string, object> ingredient =
                            rawIngredient as Dictionary<string, object>;
                        if (ingredient == null)
                            continue;
                        string uuid = DictionaryString(
                            ingredient, "itemId");
                        int quantity = DictionaryInt(
                            ingredient, "quantity", 0);
                        if (!String.IsNullOrWhiteSpace(uuid) &&
                            quantity > 0)
                        {
                            ingredients.Add(new ItemRecipeIngredient
                            {
                                Uuid = uuid,
                                Quantity = quantity
                            });
                        }
                    }
                    if (ingredients.Count == 0)
                        continue;
                    recipes.Add(new ItemRecipe
                    {
                        Uuid = output,
                        Quantity = outputQuantity,
                        CraftTime = DictionaryInt(
                            entry, "craftTime", 0),
                        Ingredients = ingredients
                    });
                }
            }
            catch
            {
                // A future or modded recipe format is ignored individually.
            }
        }

        private static Dictionary<string, int> CalculateRecipeValues(
            IEnumerable<ItemRecipe> recipes,
            IDictionary<string, int> baseValues)
        {
            Dictionary<string, int> values =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            foreach (ItemRecipe recipe in recipes)
            {
                long total = Math.Min(recipe.CraftTime, 600) / 2;
                foreach (ItemRecipeIngredient ingredient in
                    recipe.Ingredients)
                {
                    int ingredientValue;
                    if (!baseValues.TryGetValue(
                        ingredient.Uuid, out ingredientValue))
                        ingredientValue = 40;
                    total += (long)ingredientValue *
                        ingredient.Quantity;
                    if (total > 1000000)
                    {
                        total = 1000000;
                        break;
                    }
                }

                int unitValue = (int)Math.Max(
                    1, Math.Min(100000,
                        total / Math.Max(1, recipe.Quantity)));
                int existing;
                if (!values.TryGetValue(recipe.Uuid, out existing) ||
                    unitValue < existing)
                    values[recipe.Uuid] = unitValue;
            }
            return values;
        }

        private static int BaselineRecoveryValue(
            string uuid, string itemName)
        {
            string name = (itemName ?? String.Empty).ToLowerInvariant();
            if (String.Equals(
                uuid, "b41de15e-a136-425a-a730-889b58cf4466",
                StringComparison.OrdinalIgnoreCase) ||
                name.Contains("multi component kit"))
                return 10000;
            if (String.Equals(
                uuid, "5530e6a0-4748-4926-b134-50ca9ecb9dcf",
                StringComparison.OrdinalIgnoreCase) ||
                name.Contains("component kit"))
                return 4000;
            if (ContainsAny(name,
                "warehouse key", "key card", "logbook",
                "master battery", "power core", "encryptor",
                "schematic"))
                return 6000;
            if (name.Contains("epic garment"))
                return 1800;
            if (name.Contains("rare garment"))
                return 1200;
            if (ContainsAny(name,
                "spud gun", "spudgun", "shotgun", "gatling",
                "weld tool", "connect tool", "paint tool",
                "handbook", "multitool"))
                return 1000;
            if (name.Contains("common garment"))
                return 700;
            if (ContainsAny(name,
                "circuit board", "power cell", "battery",
                "explosive", "glowstick", "ammo container"))
                return 500;
            if (ContainsAny(name,
                "broccoli", "pineapple"))
                return 300;
            if (ContainsAny(name,
                "fertilizer", "chemical", "gasoline",
                "sunshake", "revival baguette"))
                return 220;
            if (ContainsAny(name,
                "banana", "blueberry", "orange",
                "tomato", "beet", "carrot", "potato",
                "cotton", "milk"))
                return 170;
            if (ContainsAny(name,
                "metal block 3", "wood block 3",
                "concrete block 3", "metal block level 3",
                "wood block level 3", "concrete block level 3",
                "spaceship block"))
                return 160;
            if (name.Contains("seed"))
                return 140;
            if (ContainsAny(name,
                "crude oil", "oil", "water", "soil",
                "emberson", "wood block 2", "metal block 2",
                "concrete block 2", "wood block level 2",
                "metal block level 2", "concrete block level 2"))
                return 90;
            if (ContainsAny(name,
                "scrap", "wood block", "metal block",
                "concrete block", "glass block", "cardboard"))
                return 60;
            return String.IsNullOrWhiteSpace(name) ||
                name == "unknown item" ? 20 : 50;
        }

        public static string RecoveryTier(int value)
        {
            if (value >= 2000)
                return "CRITICAL VALUE";
            if (value >= 800)
                return "HIGH VALUE";
            if (value >= 300)
                return "VALUABLE";
            if (value >= 120)
                return "USEFUL";
            return "STANDARD";
        }

        private static bool ContainsAny(
            string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.IndexOf(
                    term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string DictionaryString(
            IDictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value)
                ? value as string
                : null;
        }

        private static int DictionaryInt(
            IDictionary<string, object> values,
            string key, int fallback)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(
                    value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static void ReadDescriptions(string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                string json = File.ReadAllText(path);
                int objectStart = json.IndexOf('{');
                if (objectStart < 0)
                    return;
                json = json.Substring(objectStart);

                object parsed = new JavaScriptSerializer
                {
                    MaxJsonLength = Int32.MaxValue
                }.DeserializeObject(json);
                Dictionary<string, object> root =
                    parsed as Dictionary<string, object>;
                if (root == null)
                    return;

                foreach (KeyValuePair<string, object> pair in root)
                {
                    Dictionary<string, object> values =
                        pair.Value as Dictionary<string, object>;
                    if (values == null)
                        continue;

                    object titleValue;
                    object descriptionValue;
                    string title = values.TryGetValue("title", out titleValue)
                        ? titleValue as string
                        : null;
                    string description =
                        values.TryGetValue("description", out descriptionValue)
                        ? descriptionValue as string
                        : null;
                    if (String.IsNullOrWhiteSpace(title))
                        continue;

                    ItemCatalogEntry existing;
                    if (!Items.TryGetValue(pair.Key, out existing))
                    {
                        existing = new ItemCatalogEntry();
                        Items[pair.Key] = existing;
                    }
                    existing.Name = title.Trim();
                    if (!String.IsNullOrWhiteSpace(description))
                        existing.Description = description.Trim();
                }
            }
            catch
            {
                // A missing or future language format should not block save
                // analysis. UUID fallback labels remain available.
            }
        }

        private static void ReadIconMap(string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlNodeList resources = document.SelectNodes(
                    "//Resource[@type='ResourceImageSet']");
                if (resources == null)
                    return;

                foreach (XmlNode resource in resources)
                {
                    foreach (XmlNode group in resource.SelectNodes("Group"))
                    {
                        XmlAttribute textureAttribute =
                            group.Attributes["texture"];
                        XmlAttribute sizeAttribute = group.Attributes["size"];
                        if (textureAttribute == null || sizeAttribute == null)
                            continue;

                        int width;
                        int height;
                        if (!TryPair(sizeAttribute.Value, out width, out height))
                            continue;
                        string atlas = Path.Combine(
                            Path.GetDirectoryName(path),
                            textureAttribute.Value.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(atlas))
                            continue;

                        foreach (XmlNode index in group.SelectNodes("Index"))
                        {
                            XmlAttribute nameAttribute = index.Attributes["name"];
                            XmlNode frame = index.SelectSingleNode("Frame");
                            XmlAttribute pointAttribute =
                                frame == null ? null : frame.Attributes["point"];
                            if (nameAttribute == null || pointAttribute == null)
                                continue;
                            int x;
                            int y;
                            if (!TryPair(pointAttribute.Value, out x, out y))
                                continue;

                            Icons[nameAttribute.Value] = new ItemIconLocation
                            {
                                AtlasPath = atlas,
                                X = x,
                                Y = y,
                                Width = width,
                                Height = height
                            };
                        }
                    }
                }
            }
            catch
            {
                // The item list remains usable with the CSS fallback badge.
            }
        }

        private static bool TryPair(
            string value, out int first, out int second)
        {
            first = 0;
            second = 0;
            string[] parts = (value ?? String.Empty).Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 &&
                Int32.TryParse(
                    parts[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out first) &&
                Int32.TryParse(
                    parts[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out second) &&
                first >= 0 && second >= 0;
        }

        private static string ReadIcon(string uuid)
        {
            ItemIconLocation location;
            if (String.IsNullOrEmpty(uuid) ||
                !Icons.TryGetValue(uuid, out location))
                return String.Empty;

            try
            {
                using (Image atlas = Image.FromFile(location.AtlasPath))
                {
                    if (location.X + location.Width > atlas.Width ||
                        location.Y + location.Height > atlas.Height)
                        return String.Empty;

                    using (Bitmap icon = new Bitmap(
                        location.Width, location.Height,
                        PixelFormat.Format32bppArgb))
                    using (Graphics graphics = Graphics.FromImage(icon))
                    using (MemoryStream output = new MemoryStream())
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImage(
                            atlas,
                            new Rectangle(
                                0, 0, location.Width, location.Height),
                            new Rectangle(
                                location.X, location.Y,
                                location.Width, location.Height),
                            GraphicsUnit.Pixel);
                        icon.Save(output, ImageFormat.Png);
                        return "data:image/png;base64," +
                            Convert.ToBase64String(output.ToArray());
                    }
                }
            }
            catch
            {
                return String.Empty;
            }
        }
    }
}

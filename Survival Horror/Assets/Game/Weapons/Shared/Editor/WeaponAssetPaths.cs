using System;
using System.IO;

/// <summary>Paths for weapon content, grouped by the item that owns it.</summary>
public static class WeaponAssetPaths
{
    public const string Root = "Assets/Game/Weapons/";

    public static string Owner(string name)
    {
        name = Path.GetFileName(name);
        if (name.StartsWith("Attack_", StringComparison.Ordinal)) name = name.Substring(7);
        foreach (var ammo in new[] { "HuntingRifleAmmo", "RevolverAmmo", "ShotgunAmmo", "PistolAmmo", "FlareAmmo", "SMGAmmo" })
            if (name.StartsWith(ammo, StringComparison.Ordinal)) return "Ammunition/" + ammo;
        if (name.StartsWith("RifleAmmo", StringComparison.Ordinal)) return "Ammunition/HuntingRifleAmmo";
        if (name.StartsWith("Cartridge_", StringComparison.Ordinal) || name.StartsWith("ShotShell_", StringComparison.Ordinal) || name.StartsWith("FlareCartridge_", StringComparison.Ordinal)) return "Ammunition/Shared";
        if (name.StartsWith("Flare", StringComparison.Ordinal) || name.StartsWith("SignalFlare", StringComparison.Ordinal) || name.StartsWith("StylizedFlare", StringComparison.Ordinal)) return "FlareGun";
        foreach (var item in new[] { "Revolver", "HuntingRifle", "MP5", "Pistol", "Shotgun" })
            if (name.StartsWith(item, StringComparison.Ordinal)) return item;
        if (name.StartsWith("Rifle", StringComparison.Ordinal)) return "HuntingRifle";
        return "Shared";
    }

    public static string Content(string category, string name)
    {
        name = name.Replace('\\', '/');
        if (category == "Models")
        {
            int slash = name.IndexOf('/');
            string model = slash < 0 ? name : name.Substring(0, slash);
            if (model == "AmmoSides" || model == "AmmoPackaging") return Root + "Ammunition/Shared/Models/" + name;
            return Root + Owner(model) + "/Models" + (slash < 0 ? "" : name.Substring(slash));
        }
        if (name.StartsWith("Weapon_TestPickups", StringComparison.Ordinal)) return "Assets/Sandbox/WeaponTestRoom/Prefabs/" + name;
        return Root + Owner(name) + "/" + category + "/" + name;
    }

}

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace PvPChecks 
{
    public class Configuration 
    {
        #region ConfigVariables
        public List<int> BannedItems = new List<int>();
        public List<int> BannedBuffs = new List<int>();
        public List<int> BannedProjectiles = new List<int>();
        public List<int> PvPBuffs = new List<int>();

        public int[] AmmoIDs = new int[] 
        {
            //Bullet Item IDs
            97, 234, 278, 515, 546, 1179, 1302, 1335, 1342, 1349, 1350, 1351, 1352, 3104, 3567,
            //Arrow Item IDs
            40, 41, 47, 51, 265, 516, 545, 682, 988, 1235, 1334, 1341, 3003, 3568,
            //Rocket Item IDs
            771, 772, 773, 774,
            //Dart Item IDs
            1310, 3009, 3010, 3011,
            //Misc Ammo Item IDs
            283, 154, 1261, 1783, 1785, 1836, 931, 949, 3108
        };

        public int[] UniveralPrefixIDs = 
            {
            36, 37, 38, 39, 40, 41, 53, 54, 55, 56, 57, 59, 60, 61
        };

        //Tools cannot have these prefixes
        public int[] WeaponPrefixIDs = 
            {
            20, 44, 45, 46, 47, 48, 49, 50, 51, 76
        };

        public int[] MeleePrefixIDs = 
            {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 81
        };

        public int[] RangedPrefixIDs = 
            {
            16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 58, 82
        };

        public int[] MagicPrefixIDs = 
            {
            26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 52, 83
        };

        public List<string> RestrictedRegions = new List<string>();

        public Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            { "NotImplementedMessage", "This command has not been fully implemented yet! Refer to /pvpchecks help for more." },
            { "InvalidSyntaxDefault", "Invalid syntax. Refer to /pvpchecks help for more." },

            { "HelpCommand", "Available /pvpchecks Sub-Commands:\nhelp: Displays this help page.\n<ai|ri> <item name | ID>: Adds/removes item in banned items list.\n<ar|rr> <region name>: Adds/removes region in restricted regions.\n<ap|rp> <projectile ID>: Adds/removes projectile in banned projectiles.\n<ab|rb> <buff ID>: Adds/removes buff in banned buffs.\nlist <i|r|p|b>: Lists all banned items, restricted regions, banned projectiles, or banned buffs." },

            { "InvalidSyntaxAddDelItem", "Invalid syntax. Proper syntax: /pvpchecks <additem, removeitem> <item name>" },
            { "ItemAddAlreadyExists", "{0} has already been added to the item ban list!" },
            { "ItemRemoveNotExist", "{0} is not in the item ban list!" },
            { "SuccessAddItem", "{0} has been added to the list of banned items!" },
            { "SuccessDelItem", "{0} has been removed from the list of banned items!" },

            { "InvalidSyntaxAddDelProjectile", "Invalid syntax. Proper syntax: /pvpchecks <addproj, removeproj> <projectile name | ID> <new damage>" },
            //{ "ItemAddAlreadyExists", "{0} has already been added to the item ban list!" },
            //{ "ItemRemoveNotExist", "{0} is not in the item ban list!" },
            //{ "SuccessAddItem", "{0} has been added to the list of banned items!" },
            //{ "SuccessDelItem", "{0} has been removed from the list of banned items!" },

            { "NoSuchRegion", "{0} is not a valid region!" },
            { "InvalidSyntaxAddDelRegion", "Invalid syntax. Proper syntax: /pvpchecks <addregion, removeregion> <region name>" },
            { "RegionAddAlreadyExists", "{0} has already been added to the restricted region list!" },
            { "RegionRemoveNotExist", "{0} is not in the restricted region list!" },
            { "SuccessAddRegion", "{0} has been added to the list of restricted regions!" },
            { "SuccessDelRegion", "{0} has been removed from the list of restricted regions!" },

            { "InvalidSyntaxAddDelBuff", "Invalid syntax. Proper syntax: /pvpchecks <addbuff, removebuff> <buff id>" },
            { "InvalidBuffType", "{0} is not a valid buff ID!" },
            { "BuffRemoveNotExist", "{0} is not in the buff ban list!" },
            { "BuffAddAlreadyExist", "{0} has already been added to the buff ban list!" },
            { "SuccessAddBuff", "{0} has been added to the list of banned buffs!" },
            { "SuccessDelBuff", "{0} has been removed from the list of banned buffs!" },

            { "ListBannedItems", "The list of banned items:\n{0}" },
            { "ListBannedProjectiles", "The list of banned projectiles:\n{0}" },
            { "ListBannedBuffs", "The list of banned buffs:\n{0}" },
            { "ListBannedRegions", "The list of restricted regions:\n{0}" },
            { "ListInvalidSubCommand", "Invalid syntax. Proper syntax: /pvpchecks list <i | r | p>" },

            { "InvalidItemType", "Invalid item type!" },

            { "XCannotInPvP", "{0} is banned from PvP, please unequip!" },
            { "BuffCannotInPvP", "The following buffs are not allowed in PvP: {0}. They have been removed." },
            { "IllegalPrefix", "Illegally prefixed weapons are not allowed in PvP, please unequip!" },
            { "PrefixedAmmo", "Prefixed ammo is not allowed in PvP, please unequip!" },
            { "PrefixedArmor", "Prefixed armour is not allowed in PvP, please unequip!" },
            { "DuplicateAccessory", "Please remove the duplicate accessory for PvP: {0}" },
            { "UsedSeventhSlot", "The 7th accessory slot cannot be used in PvP." },

            // DISABLE REASONS
            { "DisableBannedItem", "Used banned item." },
            { "DisableIllegalWeapon", "Used illegal weapon." },
            { "DisablePrefixedAmmo", "Used prefixed ammo." },
            { "DisablePrefixedArmor", "Used prefixed armor." },
            { "DisableDuplicateAccessory", "Used duplicate accessories." },
            { "DisableSeventhSlot", "Used 7th accessory slot." },

        };


        #endregion ConfigVariables


        public static Configuration Read(string uri) =>
            !File.Exists(uri) ? new Configuration().Write(uri)
            : JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(uri));

        public Configuration Write(string uri)
        {
            if (!File.Exists(uri))
                (new FileInfo(uri)).Directory.Create();

            File.WriteAllText(uri,
                JsonConvert.SerializeObject(this, Formatting.Indented));

            return this;
        }
    }
}

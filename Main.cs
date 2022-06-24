using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace PvPChecks 
{
    [ApiVersion(2, 1)]
    public class Main : TerrariaPlugin 
    {
        public override string Name => "PvPChecks";
        public override string Author => "Johuan, maintained by RussianMushroom";
        public override string Description => "Bans Weapons and disables PvPers from using illegitimate stuff.";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public Configuration Config;

        public string URI = Path.Combine(TShock.SavePath, "pvpchecks.json");

        List<int> IllegalMeleePrefixes = new List<int>();
        List<int> IllegalRangedPrefixes = new List<int>();
        List<int> IllegalMagicPrefixes = new List<int>();


        public Main(Terraria.Main game) : base(game) { }

        public override void Initialize() 
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
            ServerApi.Hooks.WorldSave.Register(this, OnSave);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);


            GetDataHandlers.PlayerUpdate += OnPlayerUpdate;
            TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
        }

        protected override void Dispose(bool disposing) 
        {

            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
                ServerApi.Hooks.WorldSave.Deregister(this, OnSave);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);


                GetDataHandlers.PlayerUpdate -= OnPlayerUpdate;
                TShockAPI.Hooks.GeneralHooks.ReloadEvent -= OnReload;
                Config.Write(URI);
            }

            base.Dispose(disposing);
        }


        #region Hooks
        private void OnInit(EventArgs args)
        {
            Config = Configuration.Read(URI);

            IllegalMeleePrefixes.AddRange(Config.RangedPrefixIDs);
            IllegalMeleePrefixes.AddRange(Config.MagicPrefixIDs);

            IllegalRangedPrefixes.AddRange(Config.MeleePrefixIDs);
            IllegalRangedPrefixes.AddRange(Config.MagicPrefixIDs);

            IllegalMagicPrefixes.AddRange(Config.MeleePrefixIDs);
            IllegalMagicPrefixes.AddRange(Config.RangedPrefixIDs);

            Commands.ChatCommands.Add(new Command("pvpchecks", OnCommand, "pvpchecks", "pc"));
        }

        private void OnSave(WorldSaveEventArgs arg)
        {
            Config = Config.Write(URI);
        }

        private void OnGetData(GetDataEventArgs args)
        {

        }

        #endregion Hooks

        #region DataHandlers

        private void OnReload(TShockAPI.Hooks.ReloadEventArgs args)
        {
            Config = Configuration.Read(URI);
            args?.Player?.SendSuccessMessage("[PvPChecks] Successfully reloaded config.");
        }

        public void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args) 
        {
            TSPlayer player = TShock.Players[args.PlayerId];

            if (player == null) return;

            //If the player isn't in pvp or using an item, skip pvp checking
            if (!player.TPlayer.hostile) return;

            //If the player has this permission, skip pvp checking
            if (player.HasPermission("pvpchecks.useall")) return;

            //Checks whether a player is using a banned item
            if (!player.HasPermission("pvpchecks.usebannedweps"))
            {
                if (Config.BannedItems.Contains(player.ItemInHand.netID))
                    DisableAndWarn(player,
                        Config.Messages["DisableBannedItem"],
                        Config.Messages["XCannotInPvP"].SFormat(player.ItemInHand.Name));
                if (Config.BannedItems.Contains(player.SelectedItem.netID))
                    DisableAndWarn(player,
                        Config.Messages["DisableBannedItem"],
                        Config.Messages["XCannotInPvP"].SFormat(player.SelectedItem.Name));
            }
                 

                

            //Checks whether a player has a banned buff
            if(!player.HasPermission("pvpchecks.usebannedbuffs"))
            {
                var activeBannedBuffs = player.TPlayer.buffType.Where(type => Config.BannedBuffs.Contains(type)).ToList();

                if (activeBannedBuffs.Count > 0)
                {
                    // TODO: Implement buff-saving.
                    // The buffs are removed for now and the player is notified
                    player.SendErrorMessage(Config.Messages["BuffCannotInPvP"], 
                        string.Join(", ", activeBannedBuffs.Select(id => TShock.Utils.GetBuffName(id))));
                    activeBannedBuffs.ForEach(buff => player.SetBuff(buff, 0));
                }
            }
            

            //Checks whether a player has illegal prefixed items
            if (!player.HasPermission("pvpchecks.useillegalweps")) {
                if (player.ItemInHand.maxStack > 1 || player.SelectedItem.maxStack > 1) {
                    if (player.ItemInHand.prefix != 0 || player.SelectedItem.prefix != 0)
                    {
                        DisableAndWarn(player, Config.Messages["IllegalPrefix"], Config.Messages["DisableIllegalWeapon"]);
                    }
                } else if (player.ItemInHand.melee || player.SelectedItem.melee) {
                    foreach (int prefixes in IllegalMeleePrefixes) {
                        if (player.ItemInHand.prefix == prefixes || player.SelectedItem.prefix == prefixes) {
                            DisableAndWarn(player, Config.Messages["IllegalPrefix"], Config.Messages["DisableIllegalWeapon"]);
                            break;
                        }
                    }
                } else if (player.ItemInHand.ranged || player.SelectedItem.ranged) {
                    foreach (int prefixes in IllegalRangedPrefixes) {
                        if (player.ItemInHand.prefix == prefixes || player.SelectedItem.prefix == prefixes) {
                            DisableAndWarn(player, Config.Messages["IllegalPrefix"], Config.Messages["DisableIllegalWeapon"]);
                            break;
                        }
                    }
                } else if (player.ItemInHand.magic || player.SelectedItem.magic || player.ItemInHand.summon || player.SelectedItem.summon || player.ItemInHand.DD2Summon || player.SelectedItem.DD2Summon) {
                    foreach (int prefixes in IllegalMagicPrefixes) {
                        if (player.ItemInHand.prefix == prefixes || player.SelectedItem.prefix == prefixes) {
                            DisableAndWarn(player, Config.Messages["IllegalPrefix"], Config.Messages["DisableIllegalWeapon"]);
                            break;
                        }
                    }
                }
            }

            //Checks whether a player has prefixed ammo
            if (!player.HasPermission("pvpchecks.useprefixedammo") && (player.ItemInHand.ranged || player.SelectedItem.ranged)) {
                if (player.TPlayer.inventory
                    .Where(item => Config.AmmoIDs.Contains(item.netID) && item.prefix != 0)
                    .Count() > 0)
                {
                    DisableAndWarn(player, Config.Messages["PrefixedAmmo"], Config.Messages["DisablePrefixedAmmo"]);
                }
            }

            //Checks whether a player is wearing prefixed armour
            if (!player.HasPermission("pvpchecks.useprefixedarmor")) {
                for (int index = 0; index < 3; index++) {
                    if (player.TPlayer.armor[index].prefix != 0) {
                        DisableAndWarn(player, Config.Messages["PrefixedArmor"], Config.Messages["DisablePrefixedArmor"]);
                        break;
                    }
                }
            }

            // Checks whether a player is wearing duplicate accessories/armor
            // To all you code diggers, the bool in the Dictionary serves no purpose here
            if (!player.HasPermission("pvpchecks.havedupeaccessories")) {
                List<int> duplicate = new List<int>();
                foreach (Item equips in player.TPlayer.armor) {
                    if (duplicate.Contains(equips.netID)) {
                        DisableAndWarn(player, 
                            Config.Messages["DuplicateAccessory"].SFormat(equips.Name), 
                            Config.Messages["DisableDuplicateAccessory"]);
                        break;
                    } else if (equips.netID != 0) {
                        duplicate.Add(equips.netID);
                    }
                }
            }

            //Checks whether the player is using the unobtainable 7th accessory slot
            if (!player.HasPermission("pvpchecks.use7thslot")) {
                if (player.TPlayer.armor[9].netID != 0) {
                    DisableAndWarn(player,
                            Config.Messages["UsedSeventhSlot"],
                            Config.Messages["DisableSeventhSlot"]);
                }
            }
        }

        #endregion DataHandlers

        public void OnCommand(CommandArgs args)
        {
            switch (args.Parameters.FirstOrDefault())
            {
                case "help":
                case "h":
                    args.Player.SendInfoMessage(Config.Messages["HelpCommand"]);
                    break;

                #region Item Subcommands
                case "additem":
                case "ai":
                case "removeitem":
                case "ri":
                    {
                        bool add = args.Parameters[0][0] == 'a';

                        if (args.Parameters.Count() != 2)
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxAddDelItem"]);
                            return;

                        }

                        Item item = ItemFromString(args.Player, args.Parameters[1]);

                        if (item == null) return;

                        if (Config.BannedItems.Contains(item.netID) && add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["ItemAddAlreadyExists"], item.Name);
                            return;
                        }

                        if (!Config.BannedItems.Contains(item.netID) && !add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["ItemRemoveNotExist"], item.Name);
                            return;
                        }

                        // Add to Main.Config
                        if (add) Config.BannedItems.Add(item.netID);
                        else Config.BannedItems.Remove(item.netID);

                        args.Player.SendSuccessMessage(add ? Config.Messages["SuccessAddItem"]
                                                           : Config.Messages["SuccessDelItem"], item.Name);
                    }

                    break;
                #endregion Item Subcommands

                // TODO FIX
                #region Projectile Subcommands
                case "addproj":
                case "ap":
                case "removeproj":
                case "rp":
                    {
                        bool add = args.Parameters[0][0] == 'a';

                        if (args.Parameters.Count() != 2)
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxAddDelProjectile"]);
                            return;

                        }

                        int projID = Int32.Parse(args.Parameters[1]);

                        if (Config.BannedProjectiles.Contains(projID) && add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["ItemAddAlreadyExists"], projID);
                            return;
                        }

                        if (!Config.BannedProjectiles.Contains(projID) && !add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["ItemRemoveNotExist"], projID);
                            return;
                        }

                        // Add to Main.Config
                        if (add) Config.BannedProjectiles.Add(projID);
                        else Config.BannedProjectiles.Remove(projID);

                        args.Player.SendSuccessMessage(add ? Config.Messages["SuccessAddItem"]
                                                           : Config.Messages["SuccessDelItem"], projID);
                    }

                    break;
                #endregion Projectile Subcommands

                #region Region Subcommand
                case "addregion":
                case "ar":
                case "removeregion":
                case "rr":
                    {
                        args.Player.SendErrorMessage(Config.Messages["NotImplementedMessage"]);
                        /*
                        bool add = args.Parameters[0][0] == 'a';

                        if (args.Parameters.Count() != 2)
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxAddDelRegion"]);
                            return;

                        }

                        Region region = TShock.Regions.GetRegionByName(args.Parameters[1]);

                        if (region == null)
                        {
                            args.Player.SendErrorMessage(Config.Messages["NoSuchRegion"], args.Parameters[1]);
                            return;
                        }

                        if (Config.RestrictedRegions.Contains(region.Name) && add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["RegionAddAlreadyExists"], region.Name);
                            return;
                        }

                        if (!Config.RestrictedRegions.Contains(region.Name) && !add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["RegionRemoveNotExist"], region.Name);
                            return;
                        }

                        // Add to Config
                        if (add) Config.RestrictedRegions.Add(region.Name);
                        else Config.RestrictedRegions.Remove(region.Name);

                        args.Player.SendSuccessMessage(add ? Config.Messages["SuccessAddRegion"]
                                                           : Config.Messages["SuccessDelRegion"], region.Name);

                    }
                    */
                        break;
                    }
                #endregion Region Subcommand

                #region Buff Subcommand
                case "addbuff":
                case "ab":
                case "removebuff":
                case "rb":
                    {
                        bool add = args.Parameters[0][0] == 'a';

                        if (args.Parameters.Count() != 2)
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxAddDelBuff"]);
                            return;
                        }

                        int buff;

                        if (!Int32.TryParse(args.Parameters[1], out buff))
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidBuffType"], args.Parameters[1]);
                            return;
                        }

                        if (!add)
                        {
                            if (!Config.BannedBuffs.Contains(buff))
                            {
                                args.Player.SendErrorMessage(Config.Messages["BuffRemoveNotExist"], args.Parameters[1]);
                                return;
                            }

                            Config.BannedBuffs.Remove(buff);
                            args.Player.SendSuccessMessage(Config.Messages["SuccessDelBuff"], args.Parameters[1]);
                            return;
                        }

                        if (add && Config.BannedBuffs.Contains(buff))
                        {
                            args.Player.SendErrorMessage(Config.Messages["BuffAddAlreadyExist"], args.Parameters[1]);
                            return;
                        }

                        // TODO: Add check for valid buff ID
                        Config.BannedBuffs.Add(buff);
                        args.Player.SendSuccessMessage(Config.Messages["SuccessAddBuff"], TShock.Utils.GetBuffName(buff));
                        break;
                    }
                #endregion Buff Subcommand

                #region List Subcommand
                case "list":
                case "l":
                    // Defaults to showing a list of banned items if no tag is provided.
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendInfoMessage(Config.Messages["ListBannedItems"],
                            string.Join(", ", Config.BannedItems.Select(i => TShock.Utils.GetItemById(i))
                                                                       .Where(i => i != null)
                                                                       .Select(i => $"{i.Name} ({i.netID})")
                                                                       .ToList()));
                        return;
                    }

                    switch (args.Parameters[1])
                    {
                        case "-i":
                        case "i":
                        case "item":
                            args.Player.SendInfoMessage(Config.Messages["ListBannedItems"],
                            string.Join(", ", Config.BannedItems.Select(i => TShock.Utils.GetItemById(i))
                                                                       .Where(i => i != null)
                                                                       .Select(i => $"{i.Name} ({i.netID})")
                                                                       .ToList()));
                            break;

                        case "-r":
                        case "r":
                        case "region":
                            args.Player.SendInfoMessage(Config.Messages["ListBannedRegions"],
                            string.Join(", ", Config.RestrictedRegions));
                            break;

                        case "-p":
                        case "p":
                        case "proj":
                        case "projectile":
                            args.Player.SendInfoMessage(Config.Messages["ListBannedProjectiles"],
                            string.Join(", ", Config.BannedProjectiles));
                            break;
                        case "-b":
                        case "b":
                        case "buff":
                        case "buffs":
                            args.Player.SendInfoMessage(Config.Messages["ListBannedBuffs"],
                            string.Join(", ", Config.BannedBuffs.Select(id => TShock.Utils.GetBuffName(id))));
                            break;
                        default:
                            break;
                    }


                    break;
                #endregion List Subcommand

                default:
                    args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxDefault"]);
                    break;
            }
        }

        private void DisableAndWarn(TSPlayer player, string warning, string errorMessage)
        {
            player.Disable(warning, DisableFlags.WriteToLog);
            player.SendErrorMessage(errorMessage);
        }

        private Item ItemFromString(TSPlayer player, string itemName)
        {
            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemName);

            if (matchedItems.Count == 0)
            {
                player.SendErrorMessage(Config.Messages["InvalidItemType"]);
                return null;
            }

            else if (matchedItems.Count > 1)
            {
                player.SendMultipleMatchError(matchedItems.Select(i => $"{i.Name}({i.netID})"));
                return null;
            }

            return matchedItems.First();
        }
    }
}

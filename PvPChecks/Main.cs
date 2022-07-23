using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;

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

        public List<int> IllegalMeleePrefixes = new List<int>();
        public List<int> IllegalRangedPrefixes = new List<int>();
        public List<int> IllegalMagicPrefixes = new List<int>();

        public enum Infringement
        {
            BannedItem, BannedBuff, DuplicateBuff, IllegalPrefix, PrefixedArmour, PrefixedAmmo, Duplication, SeventhSlot
        }

        public Main(Terraria.Main game) : base(game) { }

        public override void Initialize() 
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
            ServerApi.Hooks.WorldSave.Register(this, OnSave);
            // ServerApi.Hooks.NetGetData.Register(this, OnGetData);

            RegionHooks.RegionEntered += OnRegionEntered;
            GetDataHandlers.PlayerUpdate += OnPlayerUpdate;
            GeneralHooks.ReloadEvent += OnReload;

            
        }
        
        protected override void Dispose(bool disposing) 
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
                ServerApi.Hooks.WorldSave.Deregister(this, OnSave);
                // ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);

                RegionHooks.RegionEntered -= OnRegionEntered;
                GetDataHandlers.PlayerUpdate -= OnPlayerUpdate;
                GeneralHooks.ReloadEvent -= OnReload;
                Config.Write(URI);
            }

            base.Dispose(disposing);
        }

        #region Hooks
        private void OnInit(EventArgs args)
        {
            Config = Configuration.Read(URI);

            // Setting up class-exclusive prefixes
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
            
            if (args.MsgID == PacketTypes.TogglePvp)
            {
                TSPlayer player = TShock.Players[args.Msg.whoAmI];
                if (player == null) return;
                
                if (args.Msg.readBuffer[4] == 1)
                {
                    OnTogglePvPOn(player);
                    return;
                }
            }

            else if (args.MsgID == PacketTypes.ProjectileNew)
            {
                if (!Config.EnableProjectileCheck) return;
                
                TSPlayer player = TShock.Players[args.Msg.whoAmI];

                if (player == null) return;

                if (!player.TPlayer.hostile) return;

                if (!player.HasPermission("pvpchecks.useall")) return;

                // TODO: Await implementation of projectile ban
                return;
            }


        }

        /// <summary>
        /// If NotifyEnterRestrictedRegion is enabled, notify player upon entering restricted region
        /// Exempt: pvpchecks.ignoreregion
        /// </summary>
        /// <param name="args"></param>
        private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
        {
            if (!Config.NotifyEnterRestrictedRegion) return;

            if (!Config.EnableRegionCheck) return;

            if (!Config.RestrictedRegions.Contains(args.Region.Name)) return;

            TSPlayer player = args.Player;

            if (player == null) return;

            if (player.HasPermission("pvpchecks.ignoreregion")) return;

            player.SendInfoMessage(Config.Messages["RegionRestricted"]);
        }

        private void OnReload(ReloadEventArgs args)
        {
            Config = Configuration.Read(URI);
            args?.Player?.SendSuccessMessage("[PvPChecks] Successfully reloaded config.");
        }

        /// <summary>
        /// Run all item checks as defined in configuration file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args) 
        {
            TSPlayer player = TShock.Players[args.PlayerId];

            if (player == null) return;

            // If the player isn't in pvp or using an item, skip pvp checking
            if (!player.TPlayer.hostile) return;

            // If the player has this permission, skip pvp checking
            if (player.HasPermission("pvpchecks.useall")) return;

            List<Item> bannedItems = new List<Item>();
            List<int> bannedBuffs = new List<int>();

            List<Infringement> infringements = new List<Infringement>();

            // Check if restricted to restricted regions
            if (Config.EnableRegionCheck && !player.HasPermission("pvpchecks.ignoreregion"))
            {
                Region region = player.CurrentRegion;

                if (region == null) return;

                if (!Config.RestrictedRegions.Contains(region.Name)) return;

            }

            // Checks whether a player has a banned buff
            // Will deal with buffs added during pvp. Permabuffs active upon togglePvP are handled by NetGetData
            if (Config.EnableBuffCheck && !player.HasPermission("pvpchecks.usebannedbuffs"))
            {
                // Check for duplicate buffs and then disable the player
                if (player.TPlayer.buffType.Count() != player.TPlayer.buffType.Distinct().Count())
                    infringements.Add(Infringement.DuplicateBuff);


                // Check for banned buffs on the player
                player.TPlayer.buffType.Distinct().Where(b => Config.BannedBuffs.Contains(b)).ForEach(b => bannedBuffs.Add(b));

                if (bannedBuffs.Count > 0)
                    infringements.Add(Infringement.BannedBuff);

            }

            Item selected = player.SelectedItem ?? player.ItemInHand;

            // Checks whether a player is using a banned item
            if (Config.EnableItemCheck && !player.HasPermission("pvpchecks.usebannedweps"))
            {
                if (selected != null)
                {
                    if (Config.BannedItems.Contains(selected.netID))
                    {
                        bannedItems.Add(selected);
                    }
                }

                bannedItems.AddRange(player.TPlayer.miscEquips.Where(e => Config.BannedItems.Contains(e.netID)));
                bannedItems.AddRange(player.TPlayer.armor.Where(a => Config.BannedItems.Contains(a.netID)));

                if (bannedItems.Count > 0)
                {
                    infringements.Add(Infringement.BannedItem);
                }
            }

            // Checks whether a player has illegal prefixed items
            if (Config.EnablePrefixCheck && !player.HasPermission("pvpchecks.useillegalweps")) {
                if (selected != null)
                {
                    if ((selected.maxStack > 1 && selected.prefix != 0) ||
                        (selected.melee && IllegalMeleePrefixes.Contains(selected.prefix)) ||
                        (selected.ranged && IllegalRangedPrefixes.Contains(selected.prefix)) ||
                        ((selected.magic || selected.summon || selected.DD2Summon) && IllegalMagicPrefixes.Contains(selected.prefix)))
                    {
                        infringements.Add(Infringement.IllegalPrefix);
                    }

                }
            }

            //Checks whether a player has prefixed ammo
            if (Config.EnablePrefixCheck && !player.HasPermission("pvpchecks.useprefixedammo") && selected.ranged) {
                if (player.TPlayer.inventory
                    .Where(item => Config.AmmoIDs.Contains(item.netID) && item.prefix != 0)
                    .Count() > 0)
                {
                    infringements.Add(Infringement.PrefixedAmmo);
                }

            }

            //Checks whether a player is wearing prefixed armour
            if (Config.EnablePrefixCheck && !player.HasPermission("pvpchecks.useprefixedarmor")) {
                for (int index = 0; index < 3; index++) {
                    if (player.TPlayer.armor[index].prefix != 0) {
                        infringements.Add(Infringement.PrefixedArmour);
                        break;
                    }
                }
            }

            // Checks whether a player is wearing duplicate accessories/armor
            if (Config.EnableDupeCheck && !player.HasPermission("pvpchecks.havedupeaccessories")) {
                if (player.TPlayer.armor.Count() != player.TPlayer.armor.Distinct().Count())
                {
                    infringements.Add(Infringement.Duplication);
                }
            }

            //Checks whether the player is using the unobtainable 7th accessory slot
            if (Config.Enable7thSlotCheck && !player.HasPermission("pvpchecks.use7thslot")) {
                if (player.TPlayer.armor[9].netID != 0) {
                    infringements.Add(Infringement.SeventhSlot);
                }
            }

            if (infringements.Count > 0)
            {
                player.Disable();

                if (Config.DisablePvP)
                    player.SetPvP(false);

                // Prepare the message every MessageDisplayDelayInMS
                if (player.ContainsData("pvpchecks_lastwarned"))
                {
                    if ((DateTime.UtcNow - DateTime.Parse(player.GetData<string>("pvpchecks_lastwarned"))).TotalMilliseconds < Config.MessageDisplayDelayInMS)
                        return;
                }

                StringBuilder sb = new StringBuilder(Config.Messages["PlayerHasInfringement"]);

                foreach (Infringement infringement in infringements)
                {
                    switch (infringement)
                    {
                        case Infringement.BannedItem:
                            sb.AppendLine(Config.Messages["BannedItems"]
                                .SFormat(string.Join(", ", bannedItems.Select(i => i.Name)))
                                );
                            break;

                        case Infringement.BannedBuff:
                            sb.AppendLine(Config.Messages["BannedBuff"]
                                .SFormat(string.Join(", ", bannedBuffs.Select(b => TShock.Utils.GetBuffName(b))))
                                );
                            break;

                        case Infringement.DuplicateBuff:
                            sb.AppendLine(Config.Messages["DuplicateBuff"]);
                            break;

                        case Infringement.IllegalPrefix:
                            sb.AppendLine(Config.Messages["IllegalPrefix"]);
                            break;

                        case Infringement.PrefixedArmour:
                            sb.AppendLine(Config.Messages["PrefixedArmor"]);
                            break;

                        case Infringement.PrefixedAmmo:
                            sb.AppendLine(Config.Messages["PrefixedAmmo"]);
                            break;

                        case Infringement.Duplication:
                            sb.AppendLine(Config.Messages["DuplicateAccessory"]);
                            break;

                        case Infringement.SeventhSlot:
                            sb.AppendLine(Config.Messages["UsedSeventhSlot"]);
                            break;
                    }
                }

                player.SetData<string>("pvpchecks_lastwarned", DateTime.UtcNow.ToString());
                player.SendErrorMessage(sb.ToString());

            }
        }

        /// <summary>
        /// If EnableRegionCheck is enabled, notify player about buff deletion upon enabling pvp
        /// Exempt: pvpchecks.ignoreregion, pvpchecks.usebannedbuffs
        /// </summary>
        /// <param name="player"></param>
        private void OnTogglePvPOn(TSPlayer player)
        {
            if (Config.EnableBuffCheck && !player.HasPermission("pvpchecks.usebannedbuffs"))
            {
                // Check if restricted to restricted regions
                if (Config.EnableRegionCheck && !player.HasPermission("pvpchecks.ignoreregion"))
                {
                    Region region = player.CurrentRegion;

                    if (region == null) return;

                    if (!Config.RestrictedRegions.Contains(region.Name)) return;

                }

                // Notify player that all banned buffs will be cancelled until pvp off
                player.SendInfoMessage(Config.Messages["BannedBuffs"]);
            }

        }
        #endregion Hooks

        private void OnCommand(CommandArgs args)
        {
            switch (args.Parameters.FirstOrDefault())
            {
                #region Help Subcommand
                case "help":
                case "h":
                    if (!args.Player.HasPermission("pvpchecks.command.help"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

                    args.Player.SendInfoMessage(Config.Messages["HelpCommand"]);
                    break;
                #endregion Help Subcommand

                #region Item Subcommands
                case "additem":
                case "ai":
                case "removeitem":
                case "ri":
                    if (!args.Player.HasPermission("pvpchecks.command.edit"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

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

                #region Projectile Subcommands
                case "addproj":
                case "ap":
                case "removeproj":
                case "rp":
                    if (!args.Player.HasPermission("pvpchecks.command.edit"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

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
                            args.Player.SendErrorMessage(Config.Messages["ProjectileAddAlreadyExists"], projID);
                            return;
                        }

                        if (!Config.BannedProjectiles.Contains(projID) && !add)
                        {
                            args.Player.SendErrorMessage(Config.Messages["ProjectileRemoveNotExist"], projID);
                            return;
                        }

                        // Add to Main.Config
                        if (add) Config.BannedProjectiles.Add(projID);
                        else Config.BannedProjectiles.Remove(projID);

                        args.Player.SendSuccessMessage(add ? Config.Messages["SuccessAddProjectile"]
                                                           : Config.Messages["SuccessDelProjectile"], projID);
                    }

                    break;
                #endregion Projectile Subcommands

                #region Region Subcommand
                case "addregion":
                case "ar":
                case "removeregion":
                case "rr":
                    if (!args.Player.HasPermission("pvpchecks.command.edit"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

                    {
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

                 
                        break;
                    }
                #endregion Region Subcommand

                #region Buff Subcommand
                case "addbuff":
                case "ab":
                case "removebuff":
                case "rb":
                    if (!args.Player.HasPermission("pvpchecks.command.edit"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

                    {
                        bool add = args.Parameters[0][0] == 'a';

                        if (args.Parameters.Count() != 2)
                        {
                            args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxAddDelBuff"]);
                            return;
                        }

                        int buff = BuffFromString(args.Player, args.Parameters[1]);

                        if (buff < 0) return;

                        string buffName = TShock.Utils.GetBuffName(buff);

                        if (!add)
                        {
                            if (!Config.BannedBuffs.Contains(buff))
                            {
                                args.Player.SendErrorMessage(Config.Messages["BuffRemoveNotExist"], buffName);
                                return;
                            }

                            Config.BannedBuffs.Remove(buff);
                            args.Player.SendSuccessMessage(Config.Messages["SuccessDelBuff"], buffName);
                            return;
                        }

                        if (add && Config.BannedBuffs.Contains(buff))
                        {
                            args.Player.SendErrorMessage(Config.Messages["BuffAddAlreadyExist"], buffName);
                            return;
                        }

                        Config.BannedBuffs.Add(buff);
                        args.Player.SendSuccessMessage(Config.Messages["SuccessAddBuff"], buffName);

                        break;
                    }
                #endregion Buff Subcommand

                #region List Subcommand
                case "list":
                case "l":
                    if (!args.Player.HasPermission("pvpchecks.command.list"))
                    {
                        args.Player.SendErrorMessage(Config.Messages["NoPermission"]);
                        return;
                    }

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
                            args.Player.SendErrorMessage(Config.Messages["ListInvalidSubCommand"]);
                            break;
                    }


                    break;
                #endregion List Subcommand

                default:
                    args.Player.SendErrorMessage(Config.Messages["InvalidSyntaxDefault"]);
                    break;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Get item from string, warn user if invalid.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="itemNameOrId"></param>
        /// <returns></returns>
        private Item ItemFromString(TSPlayer player, string itemNameOrId)
        {
            List<Item> matchedItems = TShock.Utils.GetItemByIdOrName(itemNameOrId);

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

        /// <summary>
        /// Tries to map user input to a buff id
        /// Warns user on incorrect input
        /// </summary>
        /// <param name="player"></param>
        /// <param name="buffNameOrId">Buff name or id as string</param>
        /// <returns>Buff id if successfully converted else -1</returns>
        private int BuffFromString(TSPlayer player, string buffNameOrId)
        {
            if (!Int32.TryParse(buffNameOrId, out int buff))
            {
                // Could not be parsed to string, interpreted as buff name
                List<int> matchedBuffs = TShock.Utils.GetBuffByName(buffNameOrId);

                if (matchedBuffs.Count == 0)
                {
                    player.SendErrorMessage(Config.Messages["InvalidBuffType"]);
                    return -1;
                }
                else if (matchedBuffs.Count > 1)
                {
                    player.SendMultipleMatchError(matchedBuffs.Select(i => $"{TShock.Utils.GetBuffName(i)}({i})"));
                    return -1;
                }

                return matchedBuffs.First();
            }

            // TODO: find reference in Terraria.Main or TShock for MAX_BUFFS
            if (buff >= 338 || buff < 1)
            {
                player.SendErrorMessage(Config.Messages["InvalidBuffType"]);
                return -1;
            }

            return buff;
        }

        #endregion Helper Methods
    }
}

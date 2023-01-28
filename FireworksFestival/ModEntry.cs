using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;

namespace FireworksFestival
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        // The DGA API
        private static IDynamicGameAssetsApi DGA_API;

        // Storing whether or not free gift has been received
        private static bool hasReceivedFreeGift;

        // Monitor
        private static IMonitor monitor;

        // Shop stocks
        private static Dictionary<ISalable, int[]> blueBoatStock;
        private static Dictionary<ISalable, int[]> purpleBoatStock;
        private static Dictionary<ISalable, int[]> brownBoatStock;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            var harmony = new Harmony(this.ModManifest.UniqueID);

            harmony.Patch(
               original: AccessTools.Method(typeof(Event), nameof(Event.answerDialogue)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.answerDialogue_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(FishingGame), nameof(FishingGame.gameDoneAfterFade)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.gameDoneAfterFade_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(FishingGame), nameof(FishingGame.startMe)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.startMe_Postfix))
            );

            monitor = Monitor;
        }

        /*********
        ** Private methods
        *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            DGA_API = Helper.ModRegistry.GetApi<IDynamicGameAssetsApi>("spacechase0.DynamicGameAssets");
            if (DGA_API == null)
            {
                Monitor.Log("Could not get DGA API, mod will not work", LogLevel.Error);
                return;
            }

            DGA_API.AddEmbeddedPack(this.ModManifest, Path.Combine(Helper.DirectoryPath, "assets", "dga"));
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            hasReceivedFreeGift = false;
            blueBoatStock = getBlueBoatStock();
            purpleBoatStock = getPurpleBoatStock();
            brownBoatStock = getBrownBoatStock();
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Game1.CurrentEvent == null)
            {
                return;
            }
            Monitor.Log($"{Game1.CurrentEvent.FestivalName}", LogLevel.Debug);
            if (!Game1.CurrentEvent.isSpecificFestival("summer20"))
            {
                Monitor.Log("Not my ants!", LogLevel.Debug);
                return;
            }
            if (e.Button.IsActionButton())
            {
                // Submarine warp
                if (e.Cursor.GrabTile.X == 5 && e.Cursor.GrabTile.Y == 34)
                {
                    Response[] responses2 = new Response[2]
                    {
                        new Response("Play", Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1662")),
                        new Response("Leave", Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1663"))
                    };
                    Game1.currentLocation.createQuestionDialogue(Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1681"), responses2, "fishingGame");
                    return;
                }

                // Free shaved ice
                else if ((e.Cursor.GrabTile.X == 13 || e.Cursor.GrabTile.X == 14) && e.Cursor.GrabTile.Y == 37)
                {
                    if (!hasReceivedFreeGift)
                    {
                        Game1.currentLocation.createQuestionDialogue(Game1.content.LoadString("Strings\\Locations:BeachNightMarket_GiftGiverQuestion"), Game1.currentLocation.createYesNoResponses(), "GiftGiverQuestion");
                    }
                    else
                    {
                        Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BeachNightMarket_GiftGiverEnjoy"));
                    }
                }

                // Traveling merchant
                else if (e.Cursor.GrabTile.X == 39 && e.Cursor.GrabTile.Y == 30)
                {
                    Game1.activeClickableMenu = new ShopMenu(Utility.getTravelingMerchantStock((int)(Game1.uniqueIDForThisGame + Game1.stats.DaysPlayed)), 0, "TravelerSummerNightMarket", Utility.onTravelingMerchantShopPurchase);
                }

                // Fried foods shop
                else if (e.Cursor.GrabTile.X == 19 && e.Cursor.GrabTile.Y == 33)
                {
                    Game1.activeClickableMenu = new ShopMenu(blueBoatStock);
                }

                // Fireworks shop
                else if (e.Cursor.GrabTile.X == 25 && e.Cursor.GrabTile.Y == 39)
                {
                    ShopMenu purpleShop = new ShopMenu(purpleBoatStock, 0, null, null, null, "STF.violetlizabet.FireworkShop");
                    purpleShop.portraitPerson = new NPC(new AnimatedSprite("Characters\\Birdie", 0, 16, 32), new Vector2(1088f, 3712f), "IslandWest", 3, "Birdie", datable: false, null, Game1.content.Load<Texture2D>("Portraits\\Birdie"));
                    Game1.activeClickableMenu = purpleShop;
                }

                // Fruits shop
                else if ((e.Cursor.GrabTile.X == 47 || e.Cursor.GrabTile.X == 48) && e.Cursor.GrabTile.Y == 34)
                {
                    Game1.activeClickableMenu = new ShopMenu(brownBoatStock);
                }

                // Yukata shop
                else if ((e.Cursor.GrabTile.X == 34 || e.Cursor.GrabTile.X == 35) && e.Cursor.GrabTile.Y == 15)
                {
                    Dictionary<ISalable, int[]> shopStock = new Dictionary<ISalable, int[]>();
                    shopStock.Add(new StardewValley.Object(268, 1), new int[2] { 1000, 1 });
                    Game1.activeClickableMenu = new ShopMenu(shopStock);
                }
            }            
        }

        private static void answerDialogue_Postfix(string questionKey, int answerChoice)
        {
            if (questionKey == null)
            {
                return;
            }
            if (questionKey.Equals("GiftGiverQuestion",StringComparison.OrdinalIgnoreCase))
            {
                switch (answerChoice)
                {
                    case 0:
                        if (hasReceivedFreeGift)
                        {
                            Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BeachNightMarket_GiftGiverEnjoy"));
                        }
                        else
                        {
                            Game1.player.freezePause = 1000;
                            Game1.soundBank.PlayCue("snowyStep");
                            Game1.player.addItemByMenuIfNecessaryElseHoldUp((Item)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/ShavedIce"));
                            Game1.player.modData["violetlizabet.FireworksFestival"] = "true";
                            hasReceivedFreeGift = true;
                        } 
                        break;
                    case 1:
                        Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\Locations:BeachNightMarket_GiftGiverEnjoy"));
                        break;
                }
            }
        }

        private static void startMe_Postfix()
        {
            if (Game1.currentSeason.Equals("summer",StringComparison.OrdinalIgnoreCase) && Game1.dayOfMonth == 20)
            {
                Game1.player.Position = new Vector2(14f, 15f) * 64f;
                //monitor.Log($"Player is in {Game1.currentLocation.Name}");
            }
        }

        private static void gameDoneAfterFade_Postfix()
        {
            if (Game1.currentSeason.Equals("summer", StringComparison.OrdinalIgnoreCase) && Game1.dayOfMonth == 20)
            {
                Game1.player.Position = new Vector2(5f, 36f) * 64f;
                //monitor.Log($"Player is in {Game1.currentLocation.Name}");
            }
        }

        private Dictionary<ISalable, int[]> getBlueBoatStock()
        {
            Dictionary<ISalable, int[]> stock = new Dictionary<ISalable, int[]>();
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/Takoyaki"), new int[2] { 500, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/Yakisoba"), new int[2] { 500, int.MaxValue });
            stock.Add(new StardewValley.Object(202, 1), new int[2] { 1500, 1 });
            stock.Add(new StardewValley.Object(214, 1), new int[2] { 1500, 1 });
            stock.Add(new StardewValley.Object(205, 1), new int[2] { 1500, 1 });
            return stock;
        }

        private Dictionary<ISalable, int[]> getPurpleBoatStock()
        {
            Dictionary<ISalable, int[]> stock = new Dictionary<ISalable, int[]>();
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/RedFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/OrangeFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/YellowFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/GreenFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/BlueFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/PurpleFirework"), new int[2] { 5000, int.MaxValue });
            stock.Add((ISalable)DGA_API.SpawnDGAItem("violetlizabet.FireworksFestival/WhiteFirework"), new int[2] { 5000, int.MaxValue });
            return stock;
        }

        private Dictionary<ISalable, int[]> getBrownBoatStock()
        {
            Dictionary<ISalable, int[]> stock = new Dictionary<ISalable, int[]>();
            stock.Add(new StardewValley.Object(254, 1), new int[2] { 1000, int.MaxValue });
            stock.Add(new StardewValley.Object(400, 1), new int[2] { 1000, int.MaxValue });
            stock.Add(new StardewValley.Object(398, 1), new int[2] { 1000, int.MaxValue });
            stock.Add(new StardewValley.Object(636, 1), new int[2] { 5000, 1 });
            stock.Add(new StardewValley.Object(268, 1), new int[2] { 5000, 1 });
            return stock;
        }
    }
}
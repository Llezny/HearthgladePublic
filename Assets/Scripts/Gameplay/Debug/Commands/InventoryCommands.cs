using System;
using System.Linq;
using Hearthglade.Core.Items;
using Hearthglade.Gameplay.Database;
using Hearthglade.Gameplay.UI.Menu.Inventory;
using VContainer.Unity;

namespace Hearthglade.Gameplay.Debug.Commands {
    public class InventoryCommands : IStartable {

        // Dependencies
        private readonly InventoryService inventoryService;
        private readonly ConsoleCommandsService consoleCommandsService;
        private readonly ItemCatalog catalog;
        //

        public void Start() {
            RegisterCommands();
        }

        public InventoryCommands( InventoryService inventoryService, ConsoleCommandsService consoleCommandsService, ItemCatalog catalog ) {
            this.inventoryService = inventoryService;
            this.consoleCommandsService = consoleCommandsService;
            this.catalog = catalog;
        }

        private void RegisterCommands() {
            consoleCommandsService.AddCommand("sp", AddStoneAndWood, "Adds some starting items" );
            consoleCommandsService.AddCommand("invlog", LogInventory, "Print inventory items" );
            consoleCommandsService.AddCommand("cooktest", AddCookingTestItems, "Adds Wood, Carrot and a Pot to test the cooking system" );
            consoleCommandsService.AddCommand("add", AddItem, "add [item_name] [quantity] - gives items, the name is not case sensitive" );
        }

        public string AddItem( string input = "" ) {
            var args = input.Split( new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );
            if( args.Length == 0 ) {
                return "Usage: add [item_name] [quantity]\n";
            }
            int quantity = 1;
            int nameWords = args.Length;
            if( args.Length > 1 && int.TryParse( args[ ^1 ], out var parsed ) ) {
                quantity = parsed;
                nameWords--;
            }
            if( quantity <= 0 ) {
                return "The quantity has to be greater than 0\n";
            }
            var name = string.Concat( args.Take( nameWords ) );
            var matches = catalog.All.Where( item => string.Equals( item.Id.Value, name, StringComparison.OrdinalIgnoreCase ) ).ToList();
            if( matches.Count == 0 ) {
                var similar = catalog.All.Select( item => item.Id.Value ).Where( id => id.IndexOf( name, StringComparison.OrdinalIgnoreCase ) >= 0 ).Take( 10 ).ToList();
                return similar.Count > 0 ? $"No item '{name}'. Did you mean: {string.Join( ", ", similar )}\n" : $"No item '{name}'\n";
            }
            var id = matches[ 0 ].Id;
            var result = inventoryService.AddItem( catalog.GetAsset( id ), quantity );
            return result.IsComplete ? $"Added {result.Added} x {id}\n" : $"Added {result.Added} x {id}, {result.Remainder} did not fit\n";
        }

        private void Give( string itemId, int quantity ) {
            inventoryService.AddItem( catalog.GetAsset( new ItemId( itemId ) ), quantity );
        }

        public string AddStoneAndWood( string input = "") {
            Give( "Wood", 10 );
            Give( "Stick", 10 );
            Give( "Wheat", 10 );
            Give( "Stone", 10 );
            Give( "Rope", 10 );
            Give( "Water", 10 );
            Give( "Iron", 10 );
            return "Gl bro\n";
        }

        public string LogInventory( string input = "") {
            return inventoryService.Container.ToString();
        }

        public string AddCookingTestItems( string input = "") {
            Give( "Wood", 10 );
            Give( "Carrot", 5 );
            Give( "Pot", 1 );
            return "Added Wood, Carrot and a Pot for cooking testing\n";
        }
    }
}

using System.Collections.Generic;
using Hearthglade.Core.Items;
using NUnit.Framework;

namespace Hearthglade.Core.Tests {
    public class EquipmentModelTests {

        private static void Log( List<string> log, string what, SlotType slot, ItemDefinition item ) {
            log.Add( $"{what} {slot} {item.Id}" );
        }

        private static List<string> Record( EquipmentModel model ) {
            var log = new List<string>();
            model.Equipped += ( slot, item ) => Log( log, "equip", slot, item );
            model.Unequipped += ( slot, item ) => Log( log, "unequip", slot, item );
            return log;
        }

        [ Test ]
        public void Slots_HaveTheRulesOfTheirKind( ) {
            var model = new EquipmentModel();
            Assert.IsTrue( model[ SlotType.Head ].Accepts( TestItems.Helmet() ) );
            Assert.IsFalse( model[ SlotType.Head ].Accepts( TestItems.Axe() ) );
            Assert.IsTrue( model[ SlotType.Hand ].Accepts( TestItems.Axe() ) );
            Assert.IsFalse( model[ SlotType.Hand ].Accepts( TestItems.Helmet() ) );
            Assert.IsFalse( model[ SlotType.Chest ].Accepts( TestItems.Wood() ) );
        }

        [ Test ]
        public void FillingASlot_RaisesEquipped( ) {
            var model = new EquipmentModel();
            var log = Record( model );
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Helmet() ) );
            CollectionAssert.AreEqual( new[] { "equip Head Helmet" }, log );
        }

        [ Test ]
        public void EmptyingASlot_RaisesUnequipped( ) {
            var model = new EquipmentModel();
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Helmet() ) );
            var log = Record( model );
            model[ SlotType.Head ].Clear();
            CollectionAssert.AreEqual( new[] { "unequip Head Helmet" }, log );
        }

        [ Test ]
        public void ReplacingTheItem_UnequipsTheOldOneBeforeEquippingTheNew( ) {
            var model = new EquipmentModel();
            model[ SlotType.Hand ].Set( ItemStack.Of( TestItems.Axe() ) );
            var log = Record( model );
            model[ SlotType.Hand ].Set( ItemStack.Of( TestItems.Make( "Sword", maxStack: 1, type: ItemType.Weapon ) ) );
            CollectionAssert.AreEqual( new[] { "unequip Hand Axe", "equip Hand Sword" }, log );
        }

        [ Test ]
        public void ChangingOnlyTheCountOfTheSameItem_RaisesNothing( ) {
            var model = new EquipmentModel();
            model[ SlotType.Hand ].Set( ItemStack.Of( TestItems.Make( "Dagger", maxStack: 5, type: ItemType.Weapon ), 2 ) );
            var log = Record( model );
            model[ SlotType.Hand ].Remove( 1 );
            CollectionAssert.IsEmpty( log );
        }

        [ Test ]
        public void DraggingFromTheBackpack_EquipsAndDraggingBack_Unequips( ) {
            var model = new EquipmentModel();
            var backpack = new ItemContainer( 3 );
            backpack.Add( TestItems.Helmet() );
            var log = Record( model );

            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( backpack[ 0 ], model[ SlotType.Head ] ) );
            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( model[ SlotType.Head ], backpack[ 2 ] ) );

            CollectionAssert.AreEqual( new[] { "equip Head Helmet", "unequip Head Helmet" }, log );
        }

        [ Test ]
        public void SwappingWithTheBackpack_UnequipsThenEquips( ) {
            var model = new EquipmentModel();
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Helmet() ) );
            var backpack = new ItemContainer( 2 );
            backpack.Add( TestItems.Make( "Cap", maxStack: 1, type: ItemType.Helmet ) );
            var log = Record( model );

            Assert.AreEqual( MoveResult.Swapped, ItemTransfer.Move( backpack[ 0 ], model[ SlotType.Head ] ) );

            CollectionAssert.AreEqual( new[] { "unequip Head Helmet", "equip Head Cap" }, log );
            Assert.AreEqual( "Helmet", backpack[ 0 ].Stack.Id.Value );
        }

        [ Test ]
        public void DraggingTheWrongTypeOntoASlot_IsRejectedWithoutEvents( ) {
            var model = new EquipmentModel();
            var backpack = new ItemContainer( 1 );
            backpack.Add( TestItems.Wood() );
            var log = Record( model );

            Assert.AreEqual( MoveResult.Rejected, ItemTransfer.Move( backpack[ 0 ], model[ SlotType.Head ] ) );

            CollectionAssert.IsEmpty( log );
        }

        [ Test ]
        public void RestoringSavedItems_RaisesEquippedLikeAnyOtherChange( ) {
            var model = new EquipmentModel();
            var log = Record( model );
            model[ SlotType.Chest ].Set( ItemStack.Of( TestItems.Make( "Vest", maxStack: 1, type: ItemType.Chestplate ) ) );
            CollectionAssert.AreEqual( new[] { "equip Chest Vest" }, log );
        }

        [ Test ]
        public void TryGetSlotType_FindsEquipmentSlotsOnly( ) {
            var model = new EquipmentModel();
            Assert.IsTrue( model.TryGetSlotType( model[ SlotType.Hand ], out var type ) );
            Assert.AreEqual( SlotType.Hand, type );
            Assert.IsFalse( model.TryGetSlotType( new ItemSlot(), out _ ) );
        }

        private static ItemDefinition Boots( string id = "Boots", Protection protection = default ) {
            return TestItems.Clothing( id, ItemType.Footwear, protection );
        }

        [ Test ]
        public void TheFeetSlot_TakesFootwearOnly( ) {
            var model = new EquipmentModel();
            Assert.IsTrue( model[ SlotType.Feet ].Accepts( Boots() ) );
            Assert.IsFalse( model[ SlotType.Feet ].Accepts( TestItems.Helmet() ) );
            Assert.IsFalse( model[ SlotType.Feet ].Accepts( TestItems.Axe() ) );
            Assert.IsFalse( model[ SlotType.Head ].Accepts( Boots() ) );
            Assert.IsFalse( model[ SlotType.Hand ].Accepts( Boots() ) );
        }

        [ Test ]
        public void FootwearFitsTheBackpack( ) {
            var backpack = new ItemContainer( 2 );
            Assert.IsTrue( backpack.Add( Boots() ).IsComplete );
        }

        [ Test ]
        public void DraggingBootsOntoTheFeetSlot_Equips( ) {
            var model = new EquipmentModel();
            var backpack = new ItemContainer( 2 );
            backpack.Add( Boots() );
            var log = Record( model );

            Assert.AreEqual( MoveResult.Moved, ItemTransfer.Move( backpack[ 0 ], model[ SlotType.Feet ] ) );

            CollectionAssert.AreEqual( new[] { "equip Feet Boots" }, log );
        }

        [ Test ]
        public void Protection_IsNothingWithoutClothes( ) {
            Assert.IsTrue( new EquipmentModel().Protection.IsEmpty );
        }

        [ Test ]
        public void Protection_SumsTheThreeWornPieces( ) {
            var model = new EquipmentModel();
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Clothing( "Cap", ItemType.Helmet, new Protection( cold: 1f, damage: 0.1f ) ) ) );
            model[ SlotType.Chest ].Set( ItemStack.Of( TestItems.Clothing( "Coat", ItemType.Chestplate, new Protection( cold: 2f, damage: 0.2f ) ) ) );
            model[ SlotType.Feet ].Set( ItemStack.Of( Boots( protection: new Protection( cold: 0.5f, heat: 1f, damage: 0.1f ) ) ) );

            var total = model.Protection;

            Assert.AreEqual( 3.5f, total.Cold, 1e-4f );
            Assert.AreEqual( 1f, total.Heat, 1e-4f );
            Assert.AreEqual( 0.4f, total.Damage, 1e-4f );
        }

        [ Test ]
        public void Protection_KeepsPiecesWithTheSameValuesApart( ) {
            var model = new EquipmentModel();
            var same = new Protection( cold: 0.1f );
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Clothing( "Cap", ItemType.Helmet, same ) ) );
            model[ SlotType.Feet ].Set( ItemStack.Of( Boots( protection: same ) ) );
            Assert.AreEqual( 0.2f, model.Protection.Cold, 1e-4f );

            model[ SlotType.Feet ].Clear();

            Assert.AreEqual( 0.1f, model.Protection.Cold, 1e-4f );
        }

        [ Test ]
        public void Protection_FollowsSwappingAndUnequipping( ) {
            var model = new EquipmentModel();
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Clothing( "Cap", ItemType.Helmet, new Protection( cold: 1f ) ) ) );
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Clothing( "Hood", ItemType.Helmet, new Protection( cold: 3f ) ) ) );
            Assert.AreEqual( 3f, model.Protection.Cold, 1e-4f );

            model[ SlotType.Head ].Clear();

            Assert.IsTrue( model.Protection.IsEmpty );
        }

        [ Test ]
        public void Protection_IgnoresWhatIsHeldInTheHand( ) {
            var model = new EquipmentModel();
            var shield = TestItems.Make( "Shield", maxStack: 1, type: ItemType.Weapon, protection: new Protection( damage: 0.5f ) );
            model[ SlotType.Hand ].Set( ItemStack.Of( shield ) );
            Assert.IsTrue( model.Protection.IsEmpty );
        }

        [ Test ]
        public void Protection_DamageReductionOfAFullOutfitIsCapped( ) {
            var model = new EquipmentModel();
            model[ SlotType.Head ].Set( ItemStack.Of( TestItems.Clothing( "Helm", ItemType.Helmet, new Protection( damage: 0.5f ) ) ) );
            model[ SlotType.Chest ].Set( ItemStack.Of( TestItems.Clothing( "Plate", ItemType.Chestplate, new Protection( damage: 0.6f ) ) ) );
            Assert.AreEqual( 1.1f, model.Protection.Damage, 1e-4f );
            Assert.AreEqual( Protection.MaxDamageReduction, model.Protection.DamageReduction, 1e-4f );
        }
    }
}

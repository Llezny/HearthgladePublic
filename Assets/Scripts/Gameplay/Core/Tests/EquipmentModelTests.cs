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
    }
}

namespace Hearthglade.Core.Items {
    public enum FoodType : ushort {
        None = 0,
        Vegetable = 1,
        Fruit = 2,
        Fish = 4,
        Meat = 8,
        Dairy = 16,
        Eggs = 32,
        Grain = 64,
        Sweets = 128,
        Dish = 256,
        Any = 511,
    }
}
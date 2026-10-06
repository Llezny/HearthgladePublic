using System;

namespace Hearthglade.Core.Farming {

    // What kind of food a crop is to an animal; an animal's diet is a set of these.
    [Flags]
    public enum ForageKind {
        None = 0,
        Grain = 1,
        Vegetable = 2,
        Berry = 4,
        Fruit = 8,
        Any = Grain | Vegetable | Berry | Fruit,
    }
}

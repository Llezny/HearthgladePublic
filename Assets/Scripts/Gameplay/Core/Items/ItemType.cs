
using System;

namespace Hearthglade.Core.Items {
    [Flags]
    public enum ItemType : ushort {
        None = 0,
        Resource = 1,
        Buildable = 2,
        Healing = 4,
        Food = 8,
        Tool = 16,
        Weapon = 32,
        Chestplate = 64,
        Helmet = 128,
        Everything = 255
    }
}
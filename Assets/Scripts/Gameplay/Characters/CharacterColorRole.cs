namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// What a face of a character part is, so that its colour can be chosen per character. The order is the order of
    /// ROLES in Tools/Blender/character_kit.py (the second UV set of a part holds the role of every face).
    /// </summary>
    public enum CharacterColorRole
    {
        // Eyes, straps: always as made.
        Fixed,
        Skin,
        Hair,
        // Shirt or dress.
        Top,
        // Apron, belt, hem.
        TopTrim,
        // Trousers or tights.
        Legs,
        // Hat or scarf.
        Hat,
        HatBand,
        Shoes,
    }
}

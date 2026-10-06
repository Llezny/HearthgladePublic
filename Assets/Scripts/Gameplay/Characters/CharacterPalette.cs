using UnityEngine;

namespace Hearthglade.Gameplay.Characters
{
    /// <summary>
    /// The colour sheet every model of the game is painted with (Assets/Arts/Sprites/ColorPalette/Colorsheet Tree Normal.png): 8 x 8
    /// cells of 4 x 4 pixels. A cell is numbered row * 8 + column from the top left, and a face is painted by pointing its UV at the
    /// middle of a cell.
    /// </summary>
    public static class CharacterPalette
    {
        public const int Columns = 8;
        public const int Rows = 8;
        public const int CellPixels = 4;
        public const int SheetPixels = Columns * CellPixels;

        public static Vector2 CellUv(int cell)
        {
            int row = cell / Columns;
            int column = cell % Columns;
            return new Vector2((column * CellPixels + CellPixels / 2f) / SheetPixels, 1f - (row * CellPixels + CellPixels / 2f) / SheetPixels);
        }

        public static int Cell(int row, int column)
        {
            return row * Columns + column;
        }
    }
}

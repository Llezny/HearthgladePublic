using TMPro;
using UnityEditor;
using UnityEngine.TextCore.LowLevel;

namespace Hearthglade.EditorTools
{
    // A dynamic TextMeshPro font asset collects glyphs and kerning pairs whenever the editor shows text with it, and the editor then saves them into the
    // asset: the file changes on every session although nobody touched the font. The asset is cleared of that data just before it is saved, so what goes
    // to disk (and to git) is always the clean one; the editor fills it again when needed. The data is not lost for a build either, because the asset
    // is already set to be cleared on build (m_ClearDynamicDataOnBuild).
    public class DynamicFontSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets( string[] paths )
        {
            foreach( var path in paths )
            {
                if( !path.EndsWith( ".asset" ) )
                {
                    continue;
                }
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>( path );
                if( font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic )
                {
                    font.ClearFontAssetData( true );
                }
            }
            return paths;
        }
    }
}

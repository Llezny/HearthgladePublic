using System;

namespace Hearthglade.Core.World {

    /// <summary>
    /// The key under which a scene object is kept in its block. It used to be System.HashCode.Combine, which is
    /// seeded randomly for every process: a key written to a save never matched the key computed after the next
    /// launch, so a resource gathered in a loaded game stayed in the block and came back with the chunk.
    /// This one is plain arithmetic and gives the same number on every run and platform.
    /// </summary>
    public static class SceneObjectKey {

        public static int Compute( string name,
            float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ,
            float scaX, float scaY, float scaZ ) {

            int h = SeedMixer.HashString( name );
            h = Add( h, posX ); h = Add( h, posY ); h = Add( h, posZ );
            h = Add( h, rotX ); h = Add( h, rotY ); h = Add( h, rotZ );
            h = Add( h, scaX ); h = Add( h, scaY ); h = Add( h, scaZ );
            return h;
        }

        private static int Add( int hash, float value ) {
            // -0 and 0 are the same position.
            if( value == 0f ) {
                value = 0f;
            }
            return SeedMixer.Derive( hash, BitConverter.SingleToInt32Bits( value ) );
        }
    }
}

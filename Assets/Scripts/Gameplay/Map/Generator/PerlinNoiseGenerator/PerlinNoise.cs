using System;
using Hearthglade.Core.World;
using UnityEngine;

namespace Hearthglade.Gameplay.Map.Generator.PerlinNoise
{
	// Texture helpers for the map/noise editor windows. All noise comes from Hearthglade.Core.World, the
	// same code the game uses, so what these previews show is what a map with that seed will look like.
	public class PerlinNoise : MonoBehaviour {

		public static double[,] GetPerlin(int width, int height, int octaves, float lacunarity, float persistence, int seed) {
			var noise = new GradientNoise( seed );
			double[,] noiseMap = new double[width, height];
			for (int x = 0; x < width; x++) {
				for (int y = 0; y < height; y++) {
					noiseMap[x, y] = noise.Octaves(x, y, octaves, lacunarity, persistence);
				}
			}
			return noiseMap;
		}

		public static Texture2D GetPerlinTexture(int mapWidth, int mapHeight, int octaves, float lacunarity, float persistence, int seed) {
			var noiseMap = GetPerlin(mapWidth, mapHeight, octaves, lacunarity, persistence, seed);
			return ToTexture( mapWidth, mapHeight, ( x, y ) => {
				var val = ( float ) noiseMap[ x, y ];
				return new Color( val, val, val );
			} );
		}

		public static Texture2D ToTexture( int width, int height, Func<int, int, Color> pixel ) {
			var texture = new Texture2D( width, height ) { filterMode = FilterMode.Point };
			var pixels = new Color[ width * height ];
			for( int x = 0; x < width; x++ ) {
				for( int y = 0; y < height; y++ ) {
					pixels[ y * width + x ] = pixel( x, y );
				}
			}
			texture.SetPixels( pixels );
			texture.Apply();
			return texture;
		}
	}
}

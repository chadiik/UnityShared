/*
 * 
 * Algorithm based on "A Note on 'Fast raster scan distance propagation on the discrete rectangular lattice'"
 *	F. Leymarie and M. D. Levine January 1992
 *	2nd implementation: "Fast Signed Sequential Euclidean Distance Transforms"
 *	http://www.doc.gold.ac.uk/~ffl/Refs/CompVision/DT/DTpaper.pdf
 *	https://www.academia.edu/17883225/A_Note_on_Fast_Raster_Scan_Distance_Propagation_on_the_Discrete_Rectangular_Lattice
 * 
 * The algorithm is an adapted version of Pujun Lun's c++ code, this file inherits the following copyright and permissive license:
 * 
 * MIT License: https://opensource.org/licenses/MIT
 * 
 * Copyright (c) 2018 Pujun Lun (https://github.com/lun0522/8ssedt) c++
 * Copyright (C) 2018 Chady Karlitch (http://chadiik.com) C# port and Threading (Current)
 *
 */

using System.Collections;
using System.Threading;
using UnityEngine;
using System;

namespace chadiik.algorithms {

	/// <summary>
	/// 8-points Signed Sequential Distance Transforms
	/// AKA an SDF
	/// </summary>
	/// <example> 
	/// <code> 
	///	bool[] mask = SSDT8.MaskFromRedThreshold ( maskTexture );
	/// System.Func<SSDT8> generation = new SSDT8( mask, maskTexture.width ).Generate ( radius, skew );
	/// SSDT8 ssdt8;
	/// do {
	/// 	ssdt8 = generation ();
	/// 	yield return null;
	/// }
	/// while (ssdt8 == null );
	/// ssdtTexture = ssdt8.GenerateTexture();
	///	</code>
	///	</example>
	public class SSDT8 {

		private struct Vector {

			public int dx, dy;

			public Vector ( int dx, int dy ) {
				this.dx = dx;
				this.dy = dy;
			}

			public void Copy ( Vector reference ) {
				dx = reference.dx;
				dy = reference.dy;
			}

			public int FastDistance () {
				return dx * dx + dy * dy;
			}

			public float Distance () {
				return Mathf.Sqrt ( FastDistance () );
			}

			public override string ToString () {
				return string.Format ( "Point({0}, {1})", dx, dy );
			}
		}

		private class DTProcess {

			public Vector[] grid;

			private static Vector ON = new Vector(0, 0), OFF = new Vector(DISCARD, DISCARD);

			private object m_Sync = new object();
			private int m_CompletedCount;
			private int m_NumParallel;
			private int m_MaxParallel;
			private int m_ProcessStage;
			private bool[] m_Mask;
			private bool m_MaskCondition;
			private int m_Width;

			/// <summary>
			/// MultiThreaded Distance Transform calculation
			/// </summary>
			/// <param name="mask"></param>
			/// <param name="sourceWidth"></param>
			/// <param name="maskCondition">Basically true/false for inside/outside</param>
			public DTProcess ( bool [] mask, int sourceWidth, bool maskCondition ) {

				m_Mask = mask;
				m_MaskCondition = maskCondition;
				m_Width = sourceWidth;
			}

			/// <summary>
			/// Start the process
			/// </summary>
			/// <param name="minBlockSize">Minimum number of rows per block</param>
			public void Start ( int minBlockSize ) {

				m_ProcessStage = 0;

				int numValues = m_Mask.Length;
				grid = new Vector [ numValues ];

				// initialize grid based on mask
				for ( int i = 0; i < numValues; i++ )
					grid [ i ] = m_Mask [ i ] == m_MaskCondition ? ON : OFF;

				const int pixelsPerBlock = 64;
				const int maxThreads = 24;
				int vSize = numValues / m_Width - 1;

				m_NumParallel = Mathf.Clamp ( vSize / pixelsPerBlock, 1, Mathf.Min ( maxThreads, vSize / minBlockSize ) );
				MaxThreadsUsed = m_NumParallel;

				int blockSize = vSize / m_NumParallel;

				// distribute work
				for ( int i = 0; i < m_NumParallel; i++ ) {

					int yMin = 1 + blockSize * i;
					int yMax = Mathf.Min( vSize, blockSize * i + blockSize );
					Thread thread = new Thread ( () => DT ( yMin, yMax ) );
					thread.Start ();
				}
			}

			public bool Done {
				get {

					if ( m_CompletedCount == m_NumParallel ) {
						if ( m_ProcessStage == 1 ) return true;
						m_CompletedCount = 0;
						m_ProcessStage++;

						// process further stages
						Update ();
					}

					return false;
				}
			}

			public float Progress {
				get {

					int np = m_NumParallel;
					return np == 0 ? 0f : ( float ) m_CompletedCount / np;
				}
			}

			public int MaxThreadsUsed {
				set {

					m_MaxParallel = Mathf.Max ( m_MaxParallel, value );
				}

				get {

					return m_MaxParallel;
				}
			}

			private void Update () {

				int numValues = m_Mask.Length;
				int vSize = numValues / m_Width - 1;
				int blockSize = vSize / m_NumParallel;

				// fill gaps created between blocks
				if ( m_ProcessStage == 1 ) {

					m_NumParallel--;

					// calculate a reasonable gap filler size
					int halfFillBlockSize = Mathf.Max(3, Mathf.RoundToInt( Mathf.Sqrt( blockSize )));

					// distribute work
					for ( int i = 0; i < m_NumParallel; i++ ) {

						int contact = Mathf.Min( vSize, blockSize * i + blockSize );
						int yMin = contact - halfFillBlockSize;
						int yMax = Mathf.Min( vSize, contact + halfFillBlockSize );
						Thread thread = new Thread ( () => DT ( yMin, yMax ) );
						thread.Start ();
					}
				}
			}

			private Vector Get ( int x, int y ) {

				return grid [ y * m_Width + x ];
			}

			private void Record ( ref Vector closest, int x, int y ) {

				grid [ y * m_Width + x ].Copy ( closest );
			}

			/// <summary>
			/// Helper function to find the minimum propagated distance in a 8-point region
			/// </summary>
			/// <param name="closest">Accumulator</param>
			/// <param name="x">Center</param>
			/// <param name="y">Center</param>
			/// <param name="offsetX">Convolution offset</param>
			/// <param name="offsetY">Convolution offset</param>
			private void Minimize ( ref Vector closest, int x, int y, int offsetX, int offsetY ) {

				Vector neighbour =  Get ( x + offsetX, y + offsetY );
				neighbour.dx += offsetX;
				neighbour.dy += offsetY;

				if ( neighbour.FastDistance () < closest.FastDistance () ) {
					closest.Copy ( neighbour );
				}
			}

			/// <summary>
			/// 2-pass 2-ways raster scan from yMin to yMax
			/// </summary>
			private void DT ( int yMin, int yMax ) {

				int hSize = m_Width - 1;

				// Pass 0
				for ( int y = yMin; y < yMax; y++ ) {

					for ( int x = 1; x < hSize; x++ ) {

						Vector closest = Get ( x, y );
						Minimize ( ref closest, x, y, -1, 0 );
						Minimize ( ref closest, x, y, 0, -1 );
						Minimize ( ref closest, x, y, -1, -1 );
						Minimize ( ref closest, x, y, 1, -1 );
						Record ( ref closest, x, y );
					}

					for ( int x = hSize - 1; x >= 0; x-- ) {

						Vector closest = Get ( x, y );
						Minimize ( ref closest, x, y, 1, 0 );
						Record ( ref closest, x, y );
					}
				}

				// Pass 1
				for ( int y = yMax - 1; y >= yMin; y-- ) {

					for ( int x = hSize - 1; x > 0; x-- ) {

						Vector closest = Get ( x, y );
						Minimize ( ref closest, x, y, 1, 0 );
						Minimize ( ref closest, x, y, 0, 1 );
						Minimize ( ref closest, x, y, -1, 1 );
						Minimize ( ref closest, x, y, 1, 1 );
						Record ( ref closest, x, y );
					}

					for ( int x = 1; x < m_Width; x++ ) {

						Vector closest = Get ( x, y );
						Minimize ( ref closest, x, y, -1, 0 );
						Record ( ref closest, x, y );
					}
				}

				lock ( m_Sync ) {

					m_CompletedCount++;
				}
			}
		}

		// in this context it represent infinity, assuming no distance will be larger than that one
		private const int DISCARD = 4096;

		/// <summary>
		/// Signed distance field
		/// </summary>
		public float[] values;
		public Texture2D texture;

		private int m_Width;
		private DTProcess m_Inside, m_Outside;

		/// <summary>
		/// 8-points Signed Sequential Distance Transforms
		/// AKA an SDF
		/// </summary>
		/// <param name="mask">true/false for in/out</param>
		/// <param name="sourceWidth">Texture's with</param>
		public SSDT8 ( bool [] mask, int sourceWidth ) {

			m_Width = sourceWidth;
			int height = mask.Length / m_Width;

			if ( height < 8 || m_Width < 8 ) throw new UnityException ( "Minimum size of 8x8 expected." );

			m_Inside = new DTProcess ( mask, m_Width, true );
			m_Outside = new DTProcess ( mask, m_Width, false );
		}

		/// <summary>
		/// Combined progress
		/// </summary>
		public float Progress {
			get {

				return Mathf.Min ( InsideProgress, OutsideProgress );
			}
		}

		public float InsideProgress {
			get {

				return m_Inside.Progress;
			}
		}

		public float OutsideProgress {
			get {

				return m_Outside.Progress;
			}
		}

		public float ThreadsUsed {
			get {

				return Mathf.Max ( m_Inside.MaxThreadsUsed, m_Outside.MaxThreadsUsed );
			}
		}

		/// <summary>
		/// Starts the process
		/// </summary>
		/// <param name="radius">Erosion radius</param>
		/// <param name="skew">Brightness skew factor. 1f = center (default), 2f = towards inside, 0f = towards outside</param>
		/// <returns>CompletedLoop(SSDT8)</returns>
		public Func<SSDT8> Generate ( int radius, float skew ) {

			// limit erosions distance
			float maxDistance = new Vector ( radius, radius ).Distance ();

			// minimum number of rows processed in parallel
			int minBlockSize = Mathf.CeilToInt ( maxDistance );

			// start threaded processes
			m_Inside.Start ( minBlockSize );
			m_Outside.Start ( minBlockSize );

			return () => {

				if ( m_Inside.Done && m_Outside.Done ) {

					// both processes have completed

					// cache variables
					Vector[] inside = m_Inside.grid,
					outside = m_Outside.grid;

					int numValues = inside.Length;
					values = new float [ numValues ];

					float outFactor = 2 - skew;

					// merge inside and outside with skews, and normalize to radius
					for ( int i = 0; i < numValues; i++ ) {

						float sdt = outside[i].Distance() * outFactor - inside[i].Distance() * skew;
						values [ i ] = Mathf.Clamp01 ( sdt / maxDistance + .5f );
					}

					return this;
				}

				return null;
			};
		}

		/// <summary>
		/// Generate a Texture2D from the calculated values/field
		/// </summary>
		public Texture2D GenerateTexture () {

			if ( values == null ) throw new UnityException ( "Distance fields have not been generated." );

			int numValues = values.Length;
			int height = numValues / m_Width;
			texture = new Texture2D ( m_Width, height, TextureFormat.RGBA32, true );
			Color32[] colors = new Color32[numValues];

			// SDF to pixels brightness
			for ( int i = 0; i < numValues; i++ ) {

				byte value = (byte)(values[i] * 255f);
				colors [ i ] = new Color32 ( value, value, value, value );
			}

			texture.SetPixels32 ( colors );
			texture.Apply ();

			return texture;
		}

		private static float SmoothStep ( float a, float b, float x ) {
			if ( x < a ) return 0f;
			else if ( x >= b ) return 1f;

			x = ( x - a ) / ( b - a );
			return ( x * x * ( 3f - 2f * x ) );
		}

		/// <summary>
		/// Sample usage, usually SDF are 'decoded' in a fragment shader, making use of texture filtering and mip-mapping
		/// </summary>
		/// <param name="source"></param>
		/// <param name="cutoffBlend">[0-128]</param>
		/// <param name="cutoff">[0-255]</param>
		/// <returns></returns>
		public static Texture2D GenerateContrours( Texture2D source, int cutoffBlend = 0, int cutoff = 127 ) {

			int width = source.width,
				height = source.height;

			int numPixels = width * height;
			Color32[] colors = new Color32[numPixels];
			Color32[] sourceColors = source.GetPixels32();

			float cutoffMin = (cutoff - cutoffBlend) / 256f;
			float cutoffMax = (cutoff + cutoffBlend) / 256f;

			for (int i = 0; i < numPixels; i++ ) {

				float value = sourceColors [ i ].r / 255f;
				float blendValue = SmoothStep( cutoffMin, cutoffMax, value );
				byte brightness = (byte) Mathf.FloorToInt( blendValue * 255f );
				colors [ i ] = new Color32 ( brightness, brightness, brightness, 255 );
			}

			Texture2D texture = new Texture2D(width, height, source.format, true);
			texture.SetPixels32 ( colors );
			texture.Apply ();

			return texture;
		}

		/// <summary>
		/// In/Out regions are represented by Bright/Dark pixels
		/// </summary>
		public static bool [] MaskFromRedThreshold ( Texture2D texture, int redThreshold = 127 ) {

			return MaskFromRedThreshold ( texture.GetPixels32 (), texture.width, redThreshold );
		}

		/// <summary>
		/// In/Out regions are represented by Bright/Dark pixels
		/// </summary>
		public static bool [] MaskFromRedThreshold ( Color32 [] colors, int width, int redThreshold = 127 ) {

			int numColors = colors.Length;
			bool[] mask = new bool[ numColors ];

			for ( int i = 0; i < colors.Length; i++ ) {

				mask [ i ] = colors [ i ].r > redThreshold;
			}

			return mask;
		}

		/// <summary>
		/// In/Out regions are represented by Visible/Transparent pixels
		/// </summary>
		public static bool [] MaskFromAlphaTexture ( Texture2D texture, int alphaThreshold = 127 ) {

			return MaskFromAlphaThreshold ( texture.GetPixels32 (), texture.width, alphaThreshold );
		}

		/// <summary>
		/// In/Out regions are represented by Visible/Transparent pixels
		/// </summary>
		public static bool [] MaskFromAlphaThreshold ( Color32 [] colors, int width, int alphaThreshold = 127 ) {

			int numColors = colors.Length;
			bool[] mask = new bool[ numColors ];

			for ( int i = 0; i < colors.Length; i++ ) {

				mask [ i ] = colors [ i ].a > alphaThreshold;
			}

			return mask;
		}

	}
}
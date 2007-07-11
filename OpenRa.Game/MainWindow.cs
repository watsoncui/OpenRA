using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using BluntDirectX.Direct3D;
using OpenRa.FileFormats;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenRa.Game
{
	class MainWindow : Form
	{
		readonly Renderer renderer;

		readonly Map map;
		readonly TileSet tileSet;
		
		Palette pal;
		Package TileMix;
		string TileSuffix;

		const string mapName = "scm12ea.ini";

		Dictionary<TileReference, SheetRectangle<Sheet>> tileMapping =
			new Dictionary<TileReference, SheetRectangle<Sheet>>();

		FvfVertexBuffer<Vertex> vertexBuffer;

		Dictionary<Sheet, IndexBuffer> drawBatches = new Dictionary<Sheet, IndexBuffer>();

		World world;
		TreeCache treeRenderer;

		void LoadTextures()
		{
			List<Sheet> tempSheets = new List<Sheet>();

			Size pageSize = new Size(1024,512);

			Provider<Sheet> sheetProvider = delegate
			{
				Sheet t = new Sheet( new Bitmap(pageSize.Width, pageSize.Height));
				tempSheets.Add(t);
				return t;
			};

			TileSheetBuilder<Sheet> builder = new TileSheetBuilder<Sheet>( pageSize, sheetProvider );

			for( int i = 0; i < map.Width; i++ )
				for (int j = 0; j < map.Height; j++)
				{
					TileReference tileRef = map.MapTiles[i + map.XOffset, j + map.YOffset];

					if (!tileMapping.ContainsKey(tileRef))
					{
						SheetRectangle<Sheet> rect = builder.AddImage(new Size(24, 24));
						Bitmap srcImage = tileSet.tiles[ tileRef.tile ].GetTile( tileRef.image );
						using (Graphics g = Graphics.FromImage(rect.sheet.bitmap))
							g.DrawImage(srcImage, rect.origin);

						tileMapping.Add(tileRef, rect);
					}
				}

			foreach (Sheet s in tempSheets)
				s.LoadTexture(renderer.Device);

			world = new World(renderer.Device);
			treeRenderer = new TreeCache(renderer.Device, map, TileMix, pal);

			foreach (TreeReference treeReference in map.Trees)
				world.Add(new Tree(treeReference, treeRenderer, map));
		}

		float U(SheetRectangle<Sheet> s, float u)
		{
			float u0 = (float)(s.origin.X + 0.5f) / (float)s.sheet.bitmap.Width;
			float u1 = (float)(s.origin.X + s.size.Width) / (float)s.sheet.bitmap.Width;

			return (u > 0) ? u1 : u0;// (1 - u) * u0 + u * u1;
		}

		float V(SheetRectangle<Sheet> s, float v)
		{
			float v0 = (float)(s.origin.Y + 0.5f) / (float)s.sheet.bitmap.Height;
			float v1 = (float)(s.origin.Y + s.size.Height) / (float)s.sheet.bitmap.Height;

			return (v > 0) ? v1 : v0;// return (1 - v) * v0 + v * v1;
		}

		void LoadVertexBuffer()
		{
			Dictionary<Sheet, List<ushort>> indexMap = new Dictionary<Sheet, List<ushort>>();
			List<Vertex> vertices = new List<Vertex>();

			for (int j = 0; j < map.Height; j++)
				for (int i = 0; i < map.Width; i++)
				{
					SheetRectangle<Sheet> tile = tileMapping[map.MapTiles[i + map.XOffset, j + map.YOffset]];

					ushort offset = (ushort)vertices.Count;

					vertices.Add(new Vertex(24 * i, 24 * j, 0, U(tile, 0), V(tile, 0)));
					vertices.Add(new Vertex(24 + 24 * i, 24 * j, 0, U(tile, 1), V(tile, 0)));
					vertices.Add(new Vertex(24 * i, 24 + 24 * j, 0, U(tile, 0), V(tile, 1)));
					vertices.Add(new Vertex(24 + 24 * i, 24 + 24 * j, 0, U(tile, 1), V(tile, 1)));

					List<ushort> indexList;
					if (!indexMap.TryGetValue(tile.sheet, out indexList))
						indexMap.Add(tile.sheet, indexList = new List<ushort>());

					indexList.Add(offset);
					indexList.Add((ushort)(offset + 1));
					indexList.Add((ushort)(offset + 2));

					indexList.Add((ushort)(offset + 1));
					indexList.Add((ushort)(offset + 3));
					indexList.Add((ushort)(offset + 2));
				}

			vertexBuffer = new FvfVertexBuffer<Vertex>(renderer.Device, vertices.Count, Vertex.Format);
			vertexBuffer.SetData(vertices.ToArray());

			foreach (KeyValuePair<Sheet, List<ushort>> p in indexMap)
			{
				IndexBuffer indexBuffer = new IndexBuffer(renderer.Device, p.Value.Count);
				indexBuffer.SetData(p.Value.ToArray());
				drawBatches.Add(p.Key, indexBuffer);
			}
		}

		public MainWindow()
		{
			renderer = new Renderer(this, new Size(1280, 800), false);
			Visible = true;

			IniFile mapFile = new IniFile(File.OpenRead("../../../" + mapName));
			map = new Map(mapFile);

			Text = string.Format("OpenRA - {0} - {1}", map.Title, mapName);

			tileSet = LoadTileSet(map);

			LoadTextures();
			LoadVertexBuffer();
		}

		internal void Run()
		{
			while (Created && Visible)
			{
				Frame();
				Application.DoEvents();
			}
		}

		PointF scrollPos = new PointF(1, 5);
		PointF oldPos;
		int x1,y1;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			x1 = e.X;
			y1 = e.Y;
			oldPos = scrollPos;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (e.Button != 0)
			{
				scrollPos = oldPos;
				scrollPos.X += x1 - e.X;
				scrollPos.Y += y1 - e.Y;
			}
		}

		void Frame()
		{
			PointF r1 = new PointF(2.0f / ClientSize.Width, -2.0f / ClientSize.Height);
			PointF r2 = new PointF(-1, 1);

			renderer.BeginFrame(r1, r2, scrollPos);

			foreach (KeyValuePair<Sheet, IndexBuffer> batch in drawBatches)
				DrawTerrainBatch(batch);

			world.Draw(renderer);

			renderer.EndFrame();
		}

		void DrawTerrainBatch(KeyValuePair<Sheet, IndexBuffer> batch)
		{
			int indicesPerRow = map.Width * 6;
			int verticesPerRow = map.Width * 4;

			int visibleRows = (int)(ClientSize.Height / 24.0f + 2);

			int firstRow = (int)(scrollPos.Y / 24.0f);
			int lastRow = firstRow + visibleRows;

			if (firstRow < 0) firstRow = 0;
			if (lastRow < 0) lastRow = 0;
			if (lastRow > map.Height) lastRow = map.Height;

			renderer.DrawWithShader(ShaderQuality.Low, delegate
			{
				renderer.DrawBatch(vertexBuffer, batch.Value, 
					new Range<int>(verticesPerRow * firstRow, verticesPerRow * lastRow), 
					new Range<int>(indicesPerRow * firstRow, indicesPerRow * lastRow), 
					batch.Key.texture);
			});
		}

		TileSet LoadTileSet(Map currentMap)
		{
			string theaterName = currentMap.Theater;
			if (theaterName.Length > 8)
				theaterName = theaterName.Substring(0, 8);

			pal = new Palette(File.OpenRead("../../../" + theaterName + ".pal"));
			TileMix = new Package("../../../" + theaterName + ".mix");
			TileSuffix = "." + theaterName.Substring(0, 3);

			return new TileSet(TileMix, TileSuffix, pal);
		}
	}
}

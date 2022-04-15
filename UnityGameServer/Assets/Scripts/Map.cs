using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

public static class Map
{
	//private const int xSize = 30;
	//private const int ySize = 30;
	//private const int zSize = 30;
	//private const int alkiot = 6;
	//private static byte[] mapData = new byte[xSize * ySize * zSize];

	//// load map

	//// save map
	//public static void GenerateMap()
	//{
	//	// create map that has width of 30, height of 30 (make half of the height air) and 30
	//	int counter = 0;


	//	// just generate a cube map by the sizes specified
	//	for (int _z = 0; _z < zSize; _z++)
	//	{
	//		for (int _y = 0; _y < ySize; _y++)
	//		{
	//			for (int _x = 0; _x < xSize; _x++)
	//			{
	//				for (int i = 0; i < alkiot; i++)
	//				{
	//					switch (i)
	//					{
	//						case 0:
	//							mapData[counter] = (byte)_x;
	//							break;
	//						case 1:
	//							mapData[counter] = (byte)_y;
	//							break;
	//						case 2:
	//							mapData[counter] = (byte)_z;
	//							break;
	//						case 3:
	//							mapData[counter] = (byte)50;
	//							break;
	//						case 4:
	//							mapData[counter] = (byte)50;
	//							break;
	//						case 5:
	//							mapData[counter] = (byte)50;
	//							break;
	//						case 6:
	//							if (_z < 15)
	//								mapData[counter] = (byte)1;
	//							else
	//								mapData[counter] = (byte)0;
	//							break;
	//					}
						
	//				}
	//				counter++;
	//			}
	//		}
	//	}
	//}

	//public static void SaveMapToFile()
	//{
	//	string path = "map.mapdata";
	//	Console.WriteLine("Writing to " + path);
	//	File.WriteAllBytes(path, mapData);
		
	//}

	//// changedMapEdit
	//// changedMapRead

}

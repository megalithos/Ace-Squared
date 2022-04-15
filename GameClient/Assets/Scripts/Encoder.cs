using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Encoder
{
	static int maxLen = 255;
	public static byte[] EncodeStringToBytes(string stringToEncode)
	{
		int len;
		// allow max length of 255 strings
		if (stringToEncode.Length > maxLen)
		{
			len = maxLen;
		}
		else
		{
			len = stringToEncode.Length;
		}

		byte[] arr = new byte[len];
		
		int count = 0;
		foreach (char c in stringToEncode)
		{
			if (!(count < len))
				break;

			int unicode = c;
			if (unicode < 128)
			{
				// it is in the ascii range, append to the byte array as normal
				arr[count] = System.Convert.ToByte(c);
			}
			else
			{
				// not in the range, replace it with code for question mark
				arr[count] = System.Convert.ToByte('?');
			}
			count++;
		}

		Debug.Log("returning string length of " + arr.Length);
		return arr;
	}

	public static string DecodeByteArrayToString(byte[] arr)
	{
		if (arr.Length > 255)
			throw new System.Exception("something went horribly wrong at encoder class..");

		string myMessage = "";

		for (int i = 0; i < arr.Length; i++)
		{
			myMessage += System.Convert.ToChar(arr[i]); 
		}

		Debug.Log("returning myMessage: " + myMessage + " from encoder");
		return myMessage;
	}
}

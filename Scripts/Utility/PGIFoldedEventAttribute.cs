/**********************************************
* Power Grid Inventory
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System;

namespace PowerGridInventory.Utility
{
	/// <summary>
	/// Used to mark a field so that the custom editors for PGI
	/// components know which elements to hide under the 'Events' foldout.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class PGIFoldedEventAttribute : System.Attribute
	{

	}
}

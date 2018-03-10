/**********************************************
* Ancient Craft Games
* Copyright 2014-2017 James Clark
**********************************************/
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;


namespace Toolbox
{
	public static class TypeHelper
	{
        /// <summary>
        /// Helper class for storing an object and a
        /// path to one of its properties or fields.
        /// </summary>
        [Serializable]
        public class BindingMap
        {
            public string SourceKey;
            public UnityEngine.Object DestObj;
            public string Path;

            //helper stuff for the editor UI
            public int KeyIndex;
            public int PathIndex;

            public BindingMap(string key, int keyIndex, UnityEngine.Object destObj, string path)
            {
                SourceKey = key;
                DestObj = destObj;
                Path = path;
                KeyIndex = 0;
                PathIndex = 0;
            }

        }


        //cached results for type-finding methods
        static Assembly[] _LoadedAssemblies;
        static Dictionary<string, Type> LoadedTypes = new Dictionary<string, Type>();
        static Dictionary<Type, Type> BaseType = new Dictionary<Type, Type>();

        /// <summary>
        /// Runtime assemblies.
        /// </summary>
        public static Assembly[] LoadedAssemblies
        {
            get
            {
                if (_LoadedAssemblies == null)
                {
                    _LoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    // Remove Editor assemblies
                    var runtimeAsms = new List<Assembly>();
                    foreach (Assembly asm in _LoadedAssemblies)
                    {
                        //don't include editor assemblies... this will break during runtime!
                        if (!asm.GetName().Name.Contains("Editor"))
                            runtimeAsms.Add(asm);
                    }
                    _LoadedAssemblies = runtimeAsms.ToArray();
                }

                return _LoadedAssemblies;
            }
        }

        /// <summary>
        /// Returns the children classes of the supplied type.
        /// <param name="baseType">The base type.</param>
        /// <returns>The children classes of baseType.</returns>
        /// </summary>
        public static System.Type[] GetDerivedTypes(System.Type baseType)
        {
            // Create the derived type list
            var derivedTypes = new List<System.Type>();

            foreach (Assembly asm in LoadedAssemblies)
            {
                // Get types
                Type[] exportedTypes;

                try
                {
                    exportedTypes = asm.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    Debug.LogWarning(string.Format("Ignoring the following assembly due to type-loading errors: {0}", asm.FullName));
                    continue;
                }

                for (int i = 0; i < exportedTypes.Length; i++)
                {
                    // Get type
                    Type type = exportedTypes[i];
                    // The type is a subclass of baseType?
                    if (!type.IsAbstract && type.IsSubclassOf(baseType) && type.FullName != null)
                    {
                        derivedTypes.Add(type);
                    }
                    else if(!type.IsAbstract && IsSubclassOfRawGeneric(baseType, type) && type.FullName != null)
                    {
                        derivedTypes.Add(type);
                    }
                }
            }
            derivedTypes.Sort((Type o1, Type o2) => o1.ToString().CompareTo(o2.ToString()));
            return derivedTypes.ToArray();
        }

        /// <summary>
        /// Returns the System.Type of the supplied name.
        /// <param name="name">The type name.</param>
        /// <returns>The System.Type of the supplied string.</returns>
        /// </summary>
        public static Type GetType(string name)
        {
            // Try to get the type
            Type type = null;
            if (LoadedTypes.TryGetValue(name, out type))
                return type;

            // Try C# scripts
            type = Type.GetType(name + ",Assembly-CSharp-firstpass") ?? Type.GetType(name + ",Assembly-CSharp");

            // Try AppDomain
            if (type == null)
            {
                foreach (Assembly asm in LoadedAssemblies)
                {
                    type = asm.GetType(name);
                    if (type != null)
                        break;
                }
            }

            // Add type
            LoadedTypes.Add(name, type);
            return type;
        }

        /// <summary>
        /// Returns the base class System.Type of the supplied type.
        /// The base type its the last abstract type in the class hierarchy.
        /// <param name="targetType">The target type.</param>
        /// <returns>The base class System.Type.</returns>
        /// </summary>
        public static Type GetBaseType(Type targetType)
        {
            // Try to get the type
            Type type = null;
            if (BaseType.TryGetValue(targetType, out type))
                return type;

            {
                System.Type typeIterator = targetType;
                while (typeIterator != typeof(object))
                {
                    if (typeIterator.IsAbstract)
                        type = typeIterator;
                    typeIterator = typeIterator.BaseType;
                }
            }

            if (type == null)
                type = targetType;

            // Add type
            BaseType.Add(targetType, type);

            return type;
        }

		/// <summary>
		/// [ <c>public static object GetDefault(this Type type)</c> ]
		/// <para></para>
		/// Retrieves the default value for a given Type
		/// </summary>
		/// <param name="type">The Type for which to get the default value</param>
		/// <returns>The default value for <paramref name="type"/></returns>
		/// <remarks>
		/// If a null Type, a reference Type, or a System.Void Type is supplied, this method always returns null.  If a value type 
		/// is supplied which is not publicly visible or which contains generic parameters, this method will fail with an 
		/// exception.
		/// </remarks>
		/// <example>
		/// To use this method in its native, non-extension form, make a call like:
		/// <code>
		///     object Default = DefaultValue.GetDefault(someType);
		/// </code>
		/// To use this method in its Type-extension form, make a call like:
		/// <code>
		///     object Default = someType.GetDefault();
		/// </code>
		/// </example>
		/// <seealso cref="GetDefault&lt;T&gt;"/>
		public static object GetDefault(Type type)
		{
			// If no Type was supplied, if the Type was a reference type, or if the Type was a System.Void, return null
			if (type == null || !type.IsValueType || type == typeof(void))
				return null;
			
			// If the supplied Type has generic parameters, its default value cannot be determined
			if (type.ContainsGenericParameters)
				throw new ArgumentException(
					"{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type +
					"> contains generic parameters, so the default value cannot be retrieved");
			
			// If the Type is a primitive type, or if it is another publicly-visible value type (i.e. struct/enum), return a 
			//  default instance of the value type
			if (type.IsPrimitive || !type.IsNotPublic)
			{
				try
				{
					return Activator.CreateInstance(type);
				}
				catch (Exception e)
				{
					throw new ArgumentException(
						"{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe Activator.CreateInstance method could not " +
						"create a default instance of the supplied value type <" + type +
						"> (Inner Exception message: \"" + e.Message + "\")", e);
				}
			}
			
			// Fail with exception
			throw new ArgumentException("{" + MethodInfo.GetCurrentMethod() + "} Error:\n\nThe supplied value type <" + type + 
			                            "> is not a publicly-visible type, so the default value cannot be retrieved");
		}

		/// <summary>
		/// Returns true if 'generic' is derived from 'known' without consideration for generic type parameters.
        /// EX: IsSubclassOfRawGeneric(List;lt;gt, List;ltstring;gt) would return true.
		/// </summary>
		/// <returns><c>true</c> if is subclass of raw generic the specified generic known; otherwise, <c>false</c>.</returns>
		/// <param name="generic">Generic.</param>
		/// <param name="known">Known.</param>
		public static bool IsSubclassOfRawGeneric(Type generic, Type known) 
		{
			while(known != null && known != typeof(object)) 
			{
				var cur = known.IsGenericType ? known.GetGenericTypeDefinition() : known;
				if (generic == cur) return true;
				known = known.BaseType;
			}
			return false;
		}
		
		/// <summary>
		/// Returns the generic type without consideration for type parameters.
		/// </summary>
		/// <returns>The raw generic base class.</returns>
		/// <param name="generic">Generic.</param>
		/// <param name="known">Known.</param>
		public static Type FindRawGenericBaseClass(Type generic, Type known) 
		{
			while(known.BaseType != null && known.BaseType != typeof(object)) 
			{
				var cur = known.BaseType.IsGenericType ? known.BaseType.GetGenericTypeDefinition() : known.BaseType;
				if (generic == cur) return known.BaseType;
				known = known.BaseType;
			}
			
			return null;
		}

        /// <summary>
        /// Some unity types can appear to have their references be null but are in fact
        /// using a place-holder null-like object. This method can help compare references types
        /// to both situations.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool IsReferenceNull(object obj)
        {
            if (obj == null) return true;
            else if (obj.Equals(null)) return true;
            else return false;
        }

        /// <summary>
        /// Returns true if the typeToCheck is the same or a sub-class of baseCls.
        /// </summary>
        /// <param name="baseCls"></param>
        /// <param name="typeToCheck"></param>
        /// <returns></returns>
        public static bool IsSameOrSubclass(Type baseCls, Type typeToCheck)
        {
            //Debug.Log(typeToCheck.Name + " is same or sub of " + baseCls.Name);
            return (typeToCheck == baseCls || typeToCheck.IsSubclassOf(baseCls));
        }

        /// <summary>
        /// Returns true if the two classes are either the same or one is the base class of the other.
        /// </summary>
        /// <param name="cls1"></param>
        /// <param name="cls2"></param>
        /// <returns></returns>
        public static bool AreClasesInterchangeable(Type cls1, Type cls2)
        {
            if(cls1 == cls2) return true;
            else if(cls1.IsSubclassOf(cls2)) return true;
            else return cls2.IsSubclassOf(cls1);
        }

        /// <summary>
        /// Returns all subclasses of a type.
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static Type[] FindSubClasses(Type baseType, Assembly assembly)
        {
            return assembly.GetTypes().Where(t => t.IsSubclassOf(baseType)).ToArray();
            //return assembly.GetTypes().Where(t => t.IsAssignableFrom(baseType)).ToArray();
        }

        /// <summary>
        /// Returns all subclasses of a type found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static Type[] FindSubClasses(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++ )
                types.AddRange(FindSubClasses(baseType, allAsm[i]));
            
            return types.ToArray<Type>();
        }

        /// <summary>
        /// Returns all subclasses that implement an interface.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindInterfaceImplementations(Type baseType, Assembly assembly)
        {
            //don't forget that we have concrete classes and interfaces with the same name. Let's only get the concrete, non-generic classes
            return assembly.GetTypes().Where(t => (t.GetInterfaces().Contains(baseType) && t.IsClass && !t.IsGenericType && !t.IsAbstract)).ToArray();
        }

        /// <summary>
        /// Returns all subclasses that implement an interface found in this project.
        /// </summary>
        /// <param name="baseType"></param>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static Type[] FindInterfaceImplementations(Type baseType)
        {
            List<Type> types = new List<Type>(20);
            var allAsm = LoadedAssemblies;
            for (int i = 0; i < allAsm.Length; i++)
                types.AddRange(FindInterfaceImplementations(baseType, allAsm[i]));
            return types.ToArray<Type>();
        }
	}
}

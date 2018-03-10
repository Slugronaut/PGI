/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using System;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Place-holder converter class for surrogate selection in the serializer.
    /// Most of these convertions have not been implemented.
    /// </summary>
    public class DummyConverter : IFormatterConverter
    {
        public object Convert(object value, TypeCode typeCode)
        {
            throw new NotImplementedException();
        }

        public object Convert(object value, Type type)
        {
            return value;
        }

        public bool ToBoolean(object value)
        {
            return System.Convert.ToBoolean(value);
        }

        public byte ToByte(object value)
        {
            throw new NotImplementedException();
        }

        public char ToChar(object value)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(object value)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(object value)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(object value)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(object value)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(object value)
        {
            return System.Convert.ToInt32(value);
        }

        public long ToInt64(object value)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(object value)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(object value)
        {
            return System.Convert.ToSingle(value);
        }

        public string ToString(object value)
        {
            string s = value as string;
            if (s == null && value != null) throw new InvalidCastException("Cannot convert a object of type '" + value.GetType() + "' to a string.");
            return value as string;
        }

        public ushort ToUInt16(object value)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(object value)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(object value)
        {
            throw new NotImplementedException();
        }
    }
}

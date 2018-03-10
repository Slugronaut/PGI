/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using System;
using System.Collections.Generic;
using System.Text;
using Pantagruel.Serializer;

namespace Pantagruel.Serializer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class XmlIgnoreBaseTypeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class CustomXmlSerializationOptionsAttribute : Attribute
    {
        public XmlSerializer.SerializationOptions SerializationOptions = new XmlSerializer.SerializationOptions();        

        public CustomXmlSerializationOptionsAttribute(bool useTypeCache, bool useGraphSerialization)
        {
            SerializationOptions.UseTypeCache = useTypeCache;
            SerializationOptions.UseGraphSerialization = useGraphSerialization;            
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class XmlSerializeAsCustomTypeAttribute : Attribute
    {
    }
}

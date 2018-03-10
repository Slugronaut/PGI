/**********************************************
* Pantagruel
* Copyright 2015-2016 James Clark
**********************************************/
using UnityEngine;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Text;

namespace Pantagruel.Serializer
{
    /// <summary>
    /// Simple tools for dealing with xml-specific problems.
    /// </summary>
    public static class XmlTools
    {
        /// <summary>
        /// Removes insignificant whitespace between elements in an xml-formatted string.
        /// </summary>
        /// <param name="xml">The xml-formatted string from which to remove whitespace.</param>
        /// <returns>A new xml-formatted string with the whitespace removed.</returns>
        public static string RemoveWhitespace(string xml)
        {
            var reg = new Regex(@">\s*<");
            return reg.Replace(xml.Trim(), "><");
        }

        /// <summary>
        /// Formats the incoming text to human-readable xml standards.
        /// </summary>
        /// <returns>The xml.</returns>
        /// <param name="text">Text.</param>
        public static string BeautyXml(string text)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(text);

            MemoryStream stream = new MemoryStream();
            XmlTextWriter writer = new XmlTextWriter(stream, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            writer.IndentChar = ' ';
            writer.Indentation = 4;

            doc.WriteContentTo(writer);
            writer.Flush();
            stream.Flush();

            stream.Position = 0;

            StreamReader reader = new StreamReader(stream);
            string xml = reader.ReadToEnd();
            return xml;
        }
    }
}

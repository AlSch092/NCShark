//NCShark - By AlSch092 @ Github, thanks to @Diamondo25 for MapleShark
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NCShark
{
    public sealed class Config
    {
        public string Protocol;

        public string Interface = "";
        public ushort LowPort = 33004;
        public ushort HighPort = 35001;
        
        [System.Obsolete]
        public List<Definition> Definitions = new List<Definition>();

        [XmlIgnore]
        public bool LoadedFromFile = false;

        private static Config sInstance = null;
        internal static Config Instance
        {
            get
            {
                if (sInstance == null)
                {
                    if (!File.Exists("Config.xml"))
                    {
                        sInstance = new Config();
                        sInstance.Save();
                    }
                    else
                    {
                        using (XmlReader xr = XmlReader.Create("Config.xml"))
                        {
                            XmlSerializer xs = new XmlSerializer(typeof(Config));
                            sInstance = xs.Deserialize(xr) as Config;
                            sInstance.LoadedFromFile = true;
                        }
                    }
                }
                return sInstance;
            }
        }

        internal Definition GetDefinition(bool pOutbound, ushort pOpcode)
        {
            return DefinitionsContainer.Instance.GetDefinition( pOpcode, pOutbound);
            // return Definitions.Find(d => d.Locale == pLocale && d.Build == pBuild && d.Outbound == pOutbound && d.Opcode == pOpcode);
        }


        internal static string GetPropertiesFile(bool pOutbound, byte pLocale, ushort pVersion)
        {
            return System.Environment.CurrentDirectory + Path.DirectorySeparatorChar + "Scripts" + Path.DirectorySeparatorChar + pLocale.ToString() + Path.DirectorySeparatorChar + pVersion.ToString() + Path.DirectorySeparatorChar + (pOutbound ? "send" : "recv") + ".properties";
        }

        internal void Save()
        {
            // Remove useless definitions
            if (Definitions.Count > 0)
            {
                Definitions.RemoveAll(d =>
                {
                    return d.Locale <= 0 || d.Locale >= 11;
                });


                Definitions.ForEach(d => DefinitionsContainer.Instance.SaveDefinition(d));
                DefinitionsContainer.Instance.Save();

                Definitions.Clear();
            }

            XmlWriterSettings xws = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = true,
                OmitXmlDeclaration = true
            };
            using (XmlWriter xw = XmlWriter.Create("Config.xml", xws))
            {
                XmlSerializer xs = new XmlSerializer(typeof(Config));
                xs.Serialize(xw, this);
            }
            if (!Directory.Exists("Scripts")) Directory.CreateDirectory("Scripts");
        }
    }
}

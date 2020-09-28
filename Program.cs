using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;

namespace EGExport
{
    class Program
    {
        static void Main(string[] args)
        {
            String file = String.Empty;
            if (args.Length > 0)
            {
                file = args[0];
            }
            if (File.Exists(file) && file.Contains(".egp"))
            {
                SASeg eg = new SASeg(file);
                Console.WriteLine(eg.GetCode());
            }
            else
            {
                Console.WriteLine(file + " is not a SAS Enterprise Guide file");
            }
        }
    }

    public class SASeg
    {
        private String _file, _dir, _sasProject, _baseName = String.Empty;
        private List<String> _codeFiles = new List<String>();

        public SASeg(String file)
        {
            _file = file;
        }

        public String GetCode()
        {
            CopyToTemp();
            FindSASProject();

            return ParseCode();
        }

        private void CopyToTemp()
        {
            String tmpFileName = Path.GetTempPath() + Path.GetFileName(_file).Replace(".egp", "") + Guid.NewGuid().ToString() + ".egp";
            File.Copy(_file, tmpFileName);
            _file = tmpFileName;
        }

        /* search files in subdirs */
        static List<string> DirSearch(string sDir, List<string> files = null)
        {
            if (files == null)
                files = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    if (!files.Contains(f))
                        files.Add(f);
                }
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        if (!files.Contains(f))
                            files.Add(f);
                    }
                    DirSearch(d, files);
                }
                return files;
            }
            catch (Exception e)
            {
                return new List<string>();
            }
        }

        public static string RemoveTypeTagFromXml(string xml)
        {
            if (!string.IsNullOrEmpty(xml) && xml.Contains("xmlns"))
            {
                xml = Regex.Replace(xml, @"(?<=\bxmlns="")[^""]*", "");
            }
            return xml;
        }

        private void FindSASProject()
        {
            _dir = _file.Replace(".egp", "");
            if (!Directory.Exists(_dir))
            {
                ZipFile.ExtractToDirectory(_file, _dir);
            }

            string[] files = Directory.GetFiles(_dir);
            string[] dirs = Directory.GetDirectories(_dir);

            _codeFiles = new List<string>();

            /* project file */
            foreach (string f in files)
            {
                if (f.Contains("project.xml"))
                {
                    _sasProject = f;
                    break;
                }
            }

            /* list code tasks */
            foreach (string d in dirs)
            {
                if (d.Contains("CodeTask"))
                {
                    _codeFiles.Add(d);
                }
            }
        }

        public String ParseCode()
        {
            String projectCode = "";

            /* if sas project file has been found */
            if (_sasProject != String.Empty)
            {
                /* load xml */
                String text = File.ReadAllText(_sasProject);
                XmlDocument doc = new XmlDocument();
                XmlDocument element = new XmlDocument();
                doc.LoadXml(text);
                List<String> idOrder = new List<String>();
                XmlNodeList elements = doc.DocumentElement.GetElementsByTagName("Process");

                if (elements.Count > 0)
                {
                    foreach (XmlNode el in elements)
                    {
                        /* element xml code */
                        element.LoadXml(el.OuterXml);
                        /* order of programs */
                        idOrder.Add(element.DocumentElement.GetElementsByTagName("ID")[0].FirstChild.InnerText);
                    }
                }

                /* code task ids */
                Dictionary<String, String> idCode = new Dictionary<String, String>();
                elements = doc.DocumentElement.GetElementsByTagName("Element");
                String id;
                String code;
                if (elements.Count != 0)
                {
                    foreach (XmlNode el in elements)
                    {
                        /* eg codes, without sas code tasks */
                        if (el.OuterXml.Contains("<TaskCode>"))
                        {
                            /* id - code task pairs */
                            element.LoadXml(el.OuterXml);
                            id = element.DocumentElement.GetElementsByTagName("InputIDs")[0].FirstChild.InnerText;
                            code = element.DocumentElement.GetElementsByTagName("TaskCode")[0].FirstChild.InnerText;

                            /* remove EG macros */
                            String result = removeMacros(code);

                            if (!id.Contains("CodeTask"))
                                idCode.Add(id, result);
                        }
                    }
                }

                /* remove EG macros from code tasks */
                if (_codeFiles.Count > 0)
                {
                    foreach (String f in _codeFiles)
                    {
                        if (!File.Exists(f + "//code.sas"))
                            continue;

                        code = removeMacros(File.ReadAllText(f + "//code.sas"));
                    }
                }

                /* order code tasks in right order */
                foreach (String order in idOrder)
                {
                    if (idCode.ContainsKey(order))
                        projectCode += idCode[order] + Environment.NewLine;
                }
            }
            return projectCode;
        }

        public string removeMacros(string code)
        {
            String[] makrot = new String[] { "%_eg_" };
            Int32 mStart;
            Int32 mEnd;
            string result = "";
            bool noMacros = true;
            foreach (String makro in makrot)
            {
                while (code.IndexOf(makro) > -1)
                {
                    noMacros = false;
                    mStart = code.IndexOf(makro);
                    mEnd = code.Substring(mStart).IndexOf(";") + 1;
                    String subCode = String.Empty;
                    String preCode = String.Empty;
                    preCode = code.Substring(0, mStart);
                    subCode += code.Substring(mStart + mEnd);
                    result += preCode;
                    code = subCode;
                }
            }
            if (noMacros)
                return code;
            else
                return result + code;
        }

        public bool IsFileLocked(string filePath)
        {
            try
            {
                using (File.Open(filePath, FileMode.Open)) { }
            }
            catch (IOException e)
            {
                var errorCode = Marshal.GetHRForException(e) & ((1 << 16) - 1);

                return errorCode == 32 || errorCode == 33;
            }

            return false;
        }
    }
}
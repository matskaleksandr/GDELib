using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Runtime.Remoting.Messaging;

namespace GDELib
{
    internal class DESaver
    {
        private int all = 0;
        private int NEl = 0;
        private string pathsave = "";
        private string pathcash = "";
        private string Nfilestruct = "";
        private string Nfiledata = "";
        private DEObject DE;
        private bool TOne;
        public DESaver(DEObject _DE, string _pathsave, string NFS, string NFD, bool _TOne, string _pathcash)
        {
            DE = _DE;
            pathsave = _pathsave;
            Nfilestruct = NFS;
            Nfiledata = NFD;
            TOne = _TOne;
            pathcash = _pathcash;
        }
        public void Save()
        {
            string folderPath = Path.Combine(pathcash,"cashfile\\"); //папка кэша?
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(Path.Combine(pathsave,Nfilestruct), FileMode.OpenOrCreate)))
                {
                    using (BinaryWriter writer1 = new BinaryWriter(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.OpenOrCreate)))
                    {
                        writer.Write(Convert.ToUInt32(DE.YList.Count));
                        for (int i = 0; i < DE.YList.Count; i++)
                        {
                            writer.Write(DE.YList[i].ya.ToString());
                            if (DE.YList[i].ya == Yacheyka.type.integer)
                            {
                                if (DE.YList[i].tip1 == "")
                                {
                                    DE.YList[i].tip1 = "0";
                                }
                                writer1.Write(Convert.ToInt32(DE.YList[i].tip1));
                            }
                            if (DE.YList[i].ya == Yacheyka.type.doubl)
                            {
                                if (DE.YList[i].tip2 == "")
                                {
                                    DE.YList[i].tip2 = "0";
                                }
                                writer1.Write(Convert.ToDouble(DE.YList[i].tip2));
                            }
                            if (DE.YList[i].ya == Yacheyka.type.str)
                            {
                                if (DE.YList[i].tip3 == "")
                                {
                                    DE.YList[i].tip3 = "-";
                                }
                                writer1.Write(Convert.ToString(DE.YList[i].tip3));
                            }
                            if (DE.YList[i].ya == Yacheyka.type.booling)
                            {
                                writer1.Write(Convert.ToString(DE.YList[i].tip4));
                            }
                            if (DE.YList[i].ya == Yacheyka.type.files)
                            {
                                if (DE.YList[i].tip5 != "")
                                {
                                    if (!File.Exists(folderPath + Convert.ToString(i) + ".zip"))
                                    {
                                        using (var archive = ZipFile.Open(folderPath + Convert.ToString(i) + ".zip", ZipArchiveMode.Create))
                                        {
                                            archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                        }
                                    }
                                    else
                                    {
                                        File.Delete(folderPath + Convert.ToString(i) + ".zip");
                                        using (var archive = ZipFile.Open(folderPath + Convert.ToString(i) + ".zip", ZipArchiveMode.Create))
                                        {
                                            archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                        }
                                    }
                                    byte[] zipBytes = File.ReadAllBytes(folderPath + Convert.ToString(i) + ".zip");
                                    writer.Write(zipBytes.Length);
                                    for (int j = 0; j < zipBytes.Length; j++)
                                    {
                                        writer1.Write(zipBytes[j]);
                                    }
                                }
                                else
                                {
                                    writer.Write(0);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        public void SaveOne()
        {
            string folderPath = Path.Combine(pathcash, "cashfile\\"); //папка кэша?
            try
            {
                using (BinaryWriter writer1 = new BinaryWriter(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.OpenOrCreate)))
                {
                    writer1.Write(Convert.ToUInt32(DE.YList.Count));//длина
                    for (int i = 0; i < DE.YList.Count; i++)
                    {
                        writer1.Write(DE.YList[i].ya.ToString());//структура
                    }
                    writer1.Write("-/-/-/-/-/-");//граница
                    for (int i = 0; i < DE.YList.Count; i++)//данные
                    { 
                        if (DE.YList[i].ya == Yacheyka.type.integer)
                        {
                            if (DE.YList[i].tip1 == "")
                            {
                                DE.YList[i].tip1 = "0";
                            }
                            writer1.Write(Convert.ToInt32(DE.YList[i].tip1));
                        }
                        if (DE.YList[i].ya == Yacheyka.type.doubl)
                        {
                            if (DE.YList[i].tip2 == "")
                            {
                                DE.YList[i].tip2 = "0";
                            }
                            writer1.Write(Convert.ToDouble(DE.YList[i].tip2));
                        }
                        if (DE.YList[i].ya == Yacheyka.type.str)
                        {
                            if (DE.YList[i].tip3 == "")
                            {
                                DE.YList[i].tip3 = "-";
                            }
                            writer1.Write(Convert.ToString(DE.YList[i].tip3));
                        }
                        if (DE.YList[i].ya == Yacheyka.type.booling)
                        {
                            writer1.Write(Convert.ToString(DE.YList[i].tip4));
                        }
                        if (DE.YList[i].ya == Yacheyka.type.files)
                        {
                            if (DE.YList[i].tip5 != "")
                            {
                                if (!File.Exists(folderPath + Convert.ToString(i) + ".zip"))
                                {
                                    using (var archive = ZipFile.Open(folderPath + Convert.ToString(i) + ".zip", ZipArchiveMode.Create))
                                    {
                                        archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                    }
                                }
                                else
                                {
                                    File.Delete(folderPath + Convert.ToString(i) + ".zip");
                                    using (var archive = ZipFile.Open(folderPath + Convert.ToString(i) + ".zip", ZipArchiveMode.Create))
                                    {
                                        archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                    }
                                }
                                byte[] zipBytes = File.ReadAllBytes(folderPath + Convert.ToString(i) + ".zip");
                                writer1.Write(zipBytes.Length);//длина файла
                                //writer1.Write("-/-");//граница на файл
                                for (int j = 0; j < zipBytes.Length; j++)
                                {
                                    writer1.Write(zipBytes[j]);
                                }
                                //writer1.Write("-/-");//граница на файл
                            }
                            else
                            {
                                writer1.Write(0);
                            }
                        }
                    }
                }
                
                


            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
        public string[] OpenAll()
        {
            string[] result = new string[0];
            string folderPath = pathcash + "\\cashfile\\";
            DirectoryInfo di = new DirectoryInfo(folderPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            while (DE.YList.Count != 0)
            {
                DE.Dell(DE.YList.Count - 1);
            }
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(Path.Combine(pathsave, Nfilestruct), FileMode.Open)))
                {
                    all = reader.ReadInt32();
                    result = new string[all];
                    using (BinaryReader reader1 = new BinaryReader(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Open)))
                    {
                        for (int i = 0; i < all; i++)
                        {
                            string str = reader.ReadString();
                            if (str == "integer")
                            {
                                DE.CreateCell("int", reader1.ReadInt32().ToString());
                                result[i] = DE.YList[i].tip1;
                            }
                            if (str == "doubl")
                            {
                                DE.CreateCell("double", reader1.ReadDouble().ToString());
                                result[i] = DE.YList[i].tip2;
                            }
                            if (str == "str")
                            {
                                DE.CreateCell("string", reader1.ReadString().ToString());
                                result[i] = DE.YList[i].tip3;
                            }
                            if (str == "booling")
                            {
                                DE.CreateCell("bool", reader1.ReadString().ToString());
                                result[i] = DE.YList[i].tip4.ToString();
                            }
                            if (str == "files")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader1.ReadBytes(l);
                                    File.WriteAllBytes(folderPath + Convert.ToString(i) + ".zip", zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(folderPath + Convert.ToString(i) + ".zip", folderPath);
                                    //РАСПАКОВКА АРХИВА
                                    string pathfile = Path.Combine(folderPath, name);
                                    DE.CreateCell("file", pathfile);
                                    result[i] = DE.YList[i].tip5;
                                }
                                else
                                {
                                    DE.CreateCell("file", "-");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return result;
        }
        public string[] OpenAllOne()
        {
            string[] result = new string[0];
            List<string> types = new List<string>();
            string folderPath = Path.Combine(pathcash, "cashfile");
            DirectoryInfo di = new DirectoryInfo(folderPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            while (DE.YList.Count != 0)
            {
                DE.Dell(DE.YList.Count - 1);
            }
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(Path.Combine(pathsave,Nfiledata), FileMode.Open)))
                {
                    all = reader.ReadInt32();
                    for (int i = 0; i < all; i++)
                    {
                        string str = reader.ReadString();
                        types.Add(str);
                    }
                    string razd = reader.ReadString();
                    result = new string[all];
                    if (razd == "-/-/-/-/-/-")
                    {
                        for (int i = 0; i < types.Count; i++)
                        {
                            if (types[i] == "integer")
                            {
                                DE.CreateCell("int", reader.ReadInt32().ToString());
                                result[i] = DE.YList[i].tip1;
                            }
                            if (types[i] == "doubl")
                            {
                                DE.CreateCell("double", reader.ReadDouble().ToString());
                                result[i] = DE.YList[i].tip2;
                            }
                            if (types[i] == "str")
                            {
                                DE.CreateCell("string", reader.ReadString().ToString());
                                result[i] = DE.YList[i].tip3;
                            }
                            if (types[i] == "booling")
                            {
                                DE.CreateCell("bool", reader.ReadString().ToString());
                                result[i] = DE.YList[i].tip4.ToString();
                            }
                            if (types[i] == "files")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader.ReadBytes(l);
                                    File.WriteAllBytes(folderPath + Convert.ToString(i) + ".zip", zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(folderPath + Convert.ToString(i) + ".zip", folderPath);
                                    //РАСПАКОВКА АРХИВА
                                    string pathfile = Path.Combine(folderPath,name);
                                    DE.CreateCell("file", pathfile);
                                    result[i] = DE.YList[i].tip5;
                                }
                                else
                                {
                                    DE.CreateCell("file", "-");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return result;
        }
        private string UnzipFile(string zipFilePath, string outputDirectory)
        {
            string name = null;
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                if (archive.Entries.Count > 0)
                {
                    ZipArchiveEntry entry = archive.Entries[0]; // Получение первой записи
                    string entryFileName = Path.GetFileName(entry.FullName);
                    string entryFileExtension = Path.GetExtension(entryFileName);
                    string uniqueFileName = Guid.NewGuid().ToString() + entryFileExtension;
                    string entryFilePath = Path.Combine(outputDirectory, uniqueFileName);
                    // Извлечение файла
                    entry.ExtractToFile(entryFilePath, true);
                    string folderPath = Path.Combine(pathcash,"cashfile");
                    File.Move(entryFilePath, Path.Combine(folderPath,entryFileName));
                    name = entryFileName;
                    File.Delete(entryFilePath);
                }
            }
            return name;
        }
        public string OpenNext(int n = -1)
        {
            string result = "--";
            string folderPath = Path.Combine(pathcash, "cashfile");
            DirectoryInfo di = new DirectoryInfo(folderPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            while (DE.YList.Count != 0)
            {
                DE.Dell(DE.YList.Count - 1);
            }
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(Path.Combine(pathsave, Nfilestruct), FileMode.Open)))
                {
                    all = reader.ReadInt32();
                    if (n != -1 && n <= all)
                    {
                        NEl = n;
                    }
                    using (BinaryReader reader1 = new BinaryReader(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Open)))
                    {
                        for (int i = 0; i < all; i++)
                        {
                            string str = reader.ReadString();
                            if (str == "integer")
                            {
                                DE.CreateCell("int", reader1.ReadInt32().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip1;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (str == "doubl")
                            {
                                DE.CreateCell("double", reader1.ReadDouble().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip2;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (str == "str")
                            {
                                DE.CreateCell("string", reader1.ReadString().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip3;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (str == "booling")
                            {
                                DE.CreateCell("bool", reader1.ReadString().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip4.ToString();
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (str == "files")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader1.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, Convert.ToString(i) + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, Convert.ToString(i) + ".zip"), folderPath);
                                    //РАСПАКОВКА АРХИВА
                                    string pathfile = Path.Combine(folderPath, name);
                                    DE.CreateCell("file", pathfile);
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip5;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                }
                                else
                                {
                                    DE.CreateCell("file", "-");
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip5;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return NEl.ToString() + all.ToString();
        }
        public string OpenNextOne(int n = -1)
        {
            string result = "--";
            List<string> types = new List<string>();
            string folderPath = Path.Combine(pathcash, "cashfile");
            DirectoryInfo di = new DirectoryInfo(folderPath);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            while (DE.YList.Count != 0)
            {
                DE.Dell(DE.YList.Count - 1);
            }
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Open)))
                {
                    all = reader.ReadInt32();
                    if (n != -1 && n <= all)
                    {
                        NEl = n;
                    }
                    for (int i = 0; i < all; i++)
                    {
                        string str = reader.ReadString();
                        types.Add(str);
                    }
                    string razd = reader.ReadString();
                    if (razd == "-/-/-/-/-/-")
                    {
                        for (int i = 0; i < types.Count; i++)
                        {
                            //string str = reader.ReadString();
                            if (types[i] == "integer")
                            {
                                DE.CreateCell("int", reader.ReadInt32().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip1;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (types[i] == "doubl")
                            {
                                DE.CreateCell("double", reader.ReadDouble().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip2;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (types[i] == "str")
                            {
                                DE.CreateCell("string", reader.ReadString().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip3;
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (types[i] == "booling")
                            {
                                DE.CreateCell("bool", reader.ReadString().ToString());
                                if (i == NEl)
                                {
                                    result = DE.YList[i].tip4.ToString();
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (types[i] == "files")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, Convert.ToString(i) + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, Convert.ToString(i) + ".zip"), folderPath);
                                    //РАСПАКОВКА АРХИВА
                                    string pathfile = Path.Combine(folderPath, name);
                                    DE.CreateCell("file", pathfile);
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip5;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                }
                                else
                                {
                                    DE.CreateCell("file", "-");
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip5;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return NEl.ToString() + all.ToString();
        }
    }
}

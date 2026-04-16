using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace GDELib
{
    internal class DESaver
    {
        private int all = 0;
        internal int NEl = 0;
        private string pathsave = "";
        private string pathcash = "";
        private string Nfilestruct = "";
        private string Nfiledata = "";
        private DEObject DE;
        private DESMini DESM;
        private bool TOne;
        private List<ListData> listData = new List<ListData>();

        private struct ListData
        {
            public List<int> intlistS { get; }
            public int pos { get; }

            public ListData(List<int> x, int p)
            {
                intlistS = x;
                pos = p;
            }
        }
        public DESaver(DEObject _DE, string _pathsave, string NFS, string NFD, bool _TOne, string _pathcash)
        {
            DE = _DE;
            pathsave = _pathsave;
            Nfilestruct = NFS;
            Nfiledata = NFD;
            TOne = _TOne;
            pathcash = _pathcash;
            DESM = new DESMini();
        }
        public void Save()
        {
            int mpos = 0;
            List<int> intlist = new List<int>();
            bool fList = false;
            int controlStruct = 0;
            listData.Clear();
            listData = new List<ListData>();

            for (int i = 0; i < DE.YList.Count; i++)
            {
                if (DE.YList[i].ya == Yacheyka.type.integer)
                {
                    intlist.Add(Convert.ToInt32(DE.YList[i].tip1));
                    fList = true;
                    if (i == DE.YList.Count - 1)
                    {
                        if (intlist.Count >= 5)
                        {
                            ListData LD = new ListData(intlist, i - intlist.Count + 1);
                            listData.Add(LD);
                        }
                    }
                }
                else
                {
                    if (fList == true)
                    {
                        if (intlist.Count >= 5)
                        {
                            ListData LD = new ListData(intlist, i - intlist.Count);
                            listData.Add(LD);
                        }
                    }
                    fList = false;
                    intlist = new List<int>();
                }
            }

            string folderPath = Path.Combine(pathcash, "cashfile"); //папка кэша?

            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(Path.Combine(pathsave,Nfilestruct), FileMode.Create)))
                {
                    using (BinaryWriter writer1 = new BinaryWriter(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Create)))
                    {
                        writer.Write(Convert.ToUInt32(DE.YList.Count));
                        writer.Write("SVE3");
                        Random r = new Random();
                        controlStruct = (int)r.Next() / 2;
                        writer.Write(controlStruct);
                        if (DE.password != "")
                        {
                            writer.Write("p");
                            using (SHA256 sha = SHA256.Create())
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                                byte[] hash = sha.ComputeHash(bytes);

                                StringBuilder sb = new StringBuilder();
                                foreach (byte b in hash)
                                    sb.Append(b.ToString("x2"));

                                int hashInt = BitConverter.ToInt32(hash, 0);
                                controlStruct += hashInt;

                                writer1.Write(sb.ToString());                                
                            }                            
                        }
                        writer1.Write(controlStruct);
                        for (int i = 0; i < DE.YList.Count; i++)
                        {
                            switch (DE.YList[i].ya)
                            {
                                case Yacheyka.type.integer:
                                    if (listData.Count != 0)
                                    {
                                        if (listData[mpos].pos == i)
                                        {
                                            //i += listData[mpos].intlistS.Count - 1;
                                            //Console.WriteLine(listData[mpos].intlistS.Count);
                                            writer.Write("l");
                                            writer.Write((int)listData[mpos].intlistS.Count);
                                            if (listData.Count - 1 > mpos)
                                            {
                                                mpos++;
                                            }
                                            break;
                                        }
                                    }
                                    writer.Write("i");
                                    break;
                                case Yacheyka.type.doubl:
                                    writer.Write("d");
                                    break;
                                case Yacheyka.type.str:
                                    writer.Write("s");
                                    break;
                                case Yacheyka.type.booling:
                                    writer.Write("b");
                                    break;
                                case Yacheyka.type.files:
                                    writer.Write("f");
                                    break;
                                case Yacheyka.type.mas:
                                    writer.Write("m");
                                    break;
                            }
                            //writer.Write(DE.YList[i].ya.ToString());
                            mpos = 0;
                            if (DE.YList[i].ya == Yacheyka.type.integer)
                            {
                                if (listData.Count != 0)
                                {
                                    if (listData[mpos].pos == i)
                                    {
                                        i += listData[mpos].intlistS.Count - 1;

                                        int[,] list = new int[listData[mpos].intlistS.Count, 1];
                                        for (int j = 0; j < listData[mpos].intlistS.Count; j++)
                                        {
                                            list[j, 0] = listData[mpos].intlistS[j];
                                        }
                                        //Console.WriteLine(listData[mpos].intlistS.Count);

                                        DESM.SaveMatrix(list, writer1);

                                        if (listData.Count - 1 > mpos)
                                        {
                                            mpos++;
                                        }
                                        continue;
                                    }
                                }
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
                                    if (!File.Exists(Path.Combine(folderPath, i + ".zip")))
                                    {
                                        using (var archive = ZipFile.Open(Path.Combine(folderPath, i + ".zip"), ZipArchiveMode.Create))
                                        {
                                            archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                        }
                                    }
                                    else
                                    {
                                        File.Delete(Path.Combine(folderPath, i + ".zip"));
                                        using (var archive = ZipFile.Open(Path.Combine(folderPath, i + ".zip"), ZipArchiveMode.Create))
                                        {
                                            archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                        }
                                    }
                                    byte[] zipBytes = File.ReadAllBytes(Path.Combine(folderPath, i + ".zip"));
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
                            if (DE.YList[i].ya == Yacheyka.type.mas)
                            {
                                DESM.SaveMatrix(DE.YList[i].mas, writer1);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
        public void SaveOne()
        {
            bool fList = false;
            int mpos = 0;
            int controlStruct = 0;
            List<int> intlist = new List<int>();
            listData.Clear();
            listData = new List<ListData>();
            for (int i = 0; i < DE.YList.Count; i++)
            {
                if(DE.YList[i].ya == Yacheyka.type.integer)
                {
                    intlist.Add(Convert.ToInt32(DE.YList[i].tip1));
                    fList = true;
                    if(i ==  DE.YList.Count - 1)
                    {
                        if (intlist.Count >= 5)
                        {
                            ListData LD = new ListData(intlist, i - intlist.Count + 1);
                            listData.Add(LD);
                        }
                    }
                }
                else
                {
                    if(fList == true)
                    {
                        if(intlist.Count >= 5)
                        {
                            ListData LD = new ListData(intlist, i - intlist.Count);
                            listData.Add(LD);                            
                        }                        
                    }
                    fList = false;
                    intlist = new List<int>();
                }
            }
            //Console.WriteLine(listData[1].intlistS.Count);
            string folderPath = Path.Combine(pathcash, "cashfile"); //папка кэша?
            try
            {
                using (BinaryWriter writer1 = new BinaryWriter(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Create)))
                {                    
                    writer1.Write(Convert.ToUInt32(DE.YList.Count));//длина
                    writer1.Write("SVEO2");
                    Random r = new Random();
                    controlStruct = (int)r.Next() / 2;
                    writer1.Write(controlStruct);
                    if (DE.password != "")
                    {
                        writer1.Write("p");
                    }
                    for (int i = 0; i < DE.YList.Count; i++)
                    {
                        
                        switch (DE.YList[i].ya)
                        {
                            case Yacheyka.type.integer:
                                if(listData.Count != 0)
                                {
                                    if (listData[mpos].pos == i)
                                    {
                                        i += listData[mpos].intlistS.Count - 1;
                                        //Console.WriteLine(listData[mpos].intlistS.Count);
                                        writer1.Write("l");
                                        writer1.Write((int)listData[mpos].intlistS.Count);
                                        if (listData.Count - 1 > mpos)
                                        {
                                            mpos++;                                            
                                        }
                                        break;
                                    }
                                }
                                writer1.Write("i");                             
                                break;
                            case Yacheyka.type.doubl:
                                writer1.Write("d");
                                break;
                            case Yacheyka.type.str:
                                writer1.Write("s");
                                break;
                            case Yacheyka.type.booling:
                                writer1.Write("b");
                                break;
                            case Yacheyka.type.files:
                                writer1.Write("f");
                                break;
                            case Yacheyka.type.mas:
                                writer1.Write("m");
                                break;
                        }
                        //writer1.Write(DE.YList[i].ya.ToString());//структура
                    }
                    writer1.Write("/");//граница

                    string hashs = "";
                    if (DE.password != "")
                    {
                        using (SHA256 sha = SHA256.Create())
                        {
                            byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                            byte[] hash = sha.ComputeHash(bytes);

                            StringBuilder sb = new StringBuilder();
                            foreach (byte b in hash)
                                sb.Append(b.ToString("x2"));

                            //Console.WriteLine(controlStruct);
                            hashs = sb.ToString();
                            int hashInt = BitConverter.ToInt32(hash, 0);
                            controlStruct += hashInt;
                            //Console.WriteLine(controlStruct);
                        }
                    }
                    

                    mpos = 0;
                    for (int i = 0; i < DE.YList.Count; i++)//данные
                    {                        
                        if (DE.password != "" && i == 0)
                        {
                            writer1.Write(hashs);
                        }
                        if (i == 0)
                        {
                            writer1.Write(controlStruct);
                        }
                        if (DE.YList[i].ya == Yacheyka.type.integer)
                        {
                            if (listData.Count != 0)
                            {
                                if (listData[mpos].pos == i)
                                {
                                    i += listData[mpos].intlistS.Count - 1;

                                    int[,] list = new int[listData[mpos].intlistS.Count,1];
                                    for (int j = 0; j < listData[mpos].intlistS.Count; j++)
                                    {
                                        list[j, 0] = listData[mpos].intlistS[j];
                                    }

                                    DESM.SaveMatrix(list, writer1);

                                    if (listData.Count - 1 > mpos)
                                    {
                                        mpos++;
                                    }
                                    continue;
                                }
                            }
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
                                if (!File.Exists(Path.Combine(folderPath, i + ".zip")))
                                {
                                    using (var archive = ZipFile.Open(Path.Combine(folderPath, i + ".zip"), ZipArchiveMode.Create))
                                    {
                                        archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                    }
                                }
                                else
                                {
                                    File.Delete(Path.Combine(folderPath, i + ".zip"));
                                    using (var archive = ZipFile.Open(Path.Combine(folderPath, i + ".zip"), ZipArchiveMode.Create))
                                    {
                                        archive.CreateEntryFromFile(DE.YList[i].tip5, Path.GetFileName(DE.YList[i].tip5));
                                    }
                                }
                                byte[] zipBytes = File.ReadAllBytes(Path.Combine(folderPath, i + ".zip"));
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
                        if (DE.YList[i].ya == Yacheyka.type.mas)
                        {
                            DESM.SaveMatrix(DE.YList[i].mas, writer1);
                        }
                    }
                }
                
                


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
        public string[] OpenAll()
        {
            string[] result = new string[0];
            string folderPath = Path.Combine(pathcash, "cashfile");
            DirectoryInfo di = new DirectoryInfo(folderPath);
            bool SVE3 = false;
            bool fRe = false;
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
                    long pos = 0;
                    all = reader.ReadInt32();
                    pos = reader.BaseStream.Position;
                    int controlData = 0;
                    int controlStruct = 0;
                    int controlPassword = 0;

                    try
                    {
                        string format = reader.ReadString();
                        //Console.WriteLine(format);
                        if (format == "SVE3")
                        {
                            SVE3 = true;
                        }
                    }
                    catch
                    {
                        SVE3 = false;
                    }

                    if (SVE3)
                    {
                        controlStruct = reader.ReadInt32();
                    }
                    else
                    {
                        reader.BaseStream.Position = pos;
                    }

                    string pass = "";
                    result = new string[all];

                    using (BinaryReader reader1 = new BinaryReader(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Open)))
                    {
                        for (int i = 0; i < all; i++)
                        {
                            string str = reader.ReadString();
                            //Console.WriteLine(str + " - " + i);
                            if (str == "p")
                            {
                                pass = reader1.ReadString().ToString();
                                using (SHA256 sha = SHA256.Create())
                                {
                                    byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                                    byte[] hash = sha.ComputeHash(bytes);

                                    StringBuilder sb = new StringBuilder();
                                    foreach (byte b in hash)
                                        sb.Append(b.ToString("x2"));

                                    controlPassword = BitConverter.ToInt32(hash, 0);

                                    if (pass != sb.ToString())
                                    {
                                        //Console.WriteLine(pass);
                                        //Console.WriteLine("------");
                                        //Console.WriteLine(sb.ToString());
                                        Console.WriteLine("[GDEError - 0001] Incorrect password");
                                        return null;
                                    }
                                }
                                i--;
                            }
                            if (SVE3 == true && fRe == false)
                            {
                                //Console.WriteLine("-");
                                controlData = reader1.ReadInt32();
                                if (controlData != controlStruct + controlPassword)
                                {
                                    Console.WriteLine("[GDEError - 0003] Checksum violation");
                                    return null;
                                }
                                fRe = true;
                            }
                            if (str == "l")
                            {
                                //Console.WriteLine("-");
                                int k = reader.ReadInt32();
                                //Console.WriteLine(k);
                                int[,] masdata = DESM.ReadMatrix(reader1);
                                
                                for (int j = 0; j < masdata.GetLength(0); j++)
                                {
                                    DE.CreateCell(masdata[j, 0]);
                                    result[i] = DE.YList[i].tip1;
                                    i++;
                                }
                                i--;
                                //i += k - 1;
                            }
                            if (str == "integer" || str == "i")
                            {
                                DE.CreateCell("int", reader1.ReadInt32().ToString());
                                result[i] = DE.YList[i].tip1;
                            }
                            if (str == "doubl" || str == "d")
                            {
                                DE.CreateCell("double", reader1.ReadDouble().ToString());
                                result[i] = DE.YList[i].tip2;
                            }
                            if (str == "str" || str == "s")
                            {
                                DE.CreateCell("string", reader1.ReadString().ToString());
                                result[i] = DE.YList[i].tip3;
                            }
                            if (str == "booling" || str == "b")
                            {
                                DE.CreateCell("bool", reader1.ReadString().ToString());
                                result[i] = DE.YList[i].tip4.ToString();
                            }
                            if (str == "mas" || str == "m")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader1);
                                DE.CreateCell(masdata);
                                result[i] = $"matrix_{i}";
                            }
                            if (str == "files" || str == "f")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader1.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, i + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, i + ".zip"), folderPath);
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
            bool SVEO2 = false;
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
                    long pos = 0;
                    all = reader.ReadInt32();
                    pos = reader.BaseStream.Position;
                    int controlData = 0;
                    int controlStruct = 0;
                    int controlPassword = 0;

                    try
                    {
                        string format = reader.ReadString();
                        if (format == "SVEO2")
                        {
                            SVEO2 = true;
                        }
                    }
                    catch {
                        SVEO2 = false;                        
                    }

                    if (SVEO2)
                    {
                        controlStruct = reader.ReadInt32();
                    }
                    else
                    {
                        reader.BaseStream.Position = pos;
                    }

                    string pass = "";
                    bool fPass = false;

                    for (int i = 0; i < all; i++)
                    {
                        string str = reader.ReadString();
                        //Console.WriteLine(str);
                        if (str == "p")
                        {
                            fPass = true;
                            i--;
                            continue;
                        }
                        if(str == "l")
                        {
                            //Console.WriteLine(all);
                            int k = reader.ReadInt32();
                            for (int j = 0; j < k - 1; j++)
                            {
                                types.Add(str);
                            }
                            //Console.WriteLine(k);
                            i += k - 1;
                            //Console.WriteLine(all);
                        }                        
                        types.Add(str);
                    }
                    string razd = reader.ReadString();
                    //all += all2;
                    //Console.WriteLine(razd);
                    result = new string[all];
                    if (razd == "/" || razd == "-/-/-/-/-/-")
                    {
                        if (fPass)
                        {
                            pass = reader.ReadString().ToString();
                            
                            using (SHA256 sha = SHA256.Create())
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                                byte[] hash = sha.ComputeHash(bytes);

                                StringBuilder sb = new StringBuilder();
                                foreach (byte b in hash)
                                    sb.Append(b.ToString("x2"));

                                controlPassword = BitConverter.ToInt32(hash, 0);

                                if (pass != sb.ToString())
                                {
                                    Console.WriteLine("[GDEError - 0001] Incorrect password");
                                    return null;
                                }
                            }

                            
                        }
                        if (SVEO2)
                        {
                            controlData = reader.ReadInt32();
                            if (controlData != controlStruct + controlPassword)
                            {
                                Console.WriteLine("[GDEError - 0003] Checksum violation");
                                return null;
                            }
                        }
                        for (int i = 0; i < types.Count; i++)
                        {
                            //Console.WriteLine(types[i]);
                            if (types[i] == "l")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader);
                                for(int j = 0; j < masdata.GetLength(0); j++)
                                {
                                    DE.CreateCell(masdata[j,0]);
                                    result[i] = DE.YList[i].tip1;
                                    i++;
                                }
                                i--;
                            }
                            if (types[i] == "integer" || types[i] == "i")
                            {
                                DE.CreateCell("int", reader.ReadInt32().ToString());
                                result[i] = DE.YList[i].tip1;
                            }
                            if (types[i] == "doubl" || types[i] == "d")
                            {
                                DE.CreateCell("double", reader.ReadDouble().ToString());
                                result[i] = DE.YList[i].tip2;
                            }
                            if (types[i] == "str" || types[i] == "s")
                            {
                                DE.CreateCell("string", reader.ReadString().ToString());
                                result[i] = DE.YList[i].tip3;
                            }
                            if (types[i] == "booling" || types[i] == "b")
                            {
                                DE.CreateCell("bool", reader.ReadString().ToString());
                                result[i] = DE.YList[i].tip4.ToString();
                            }
                            if (types[i] == "mas" || types[i] == "m")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader);
                                DE.CreateCell(masdata);
                                result[i] = $"matrix_{i}";
                            }
                            if (types[i] == "files" || types[i] == "f")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, i + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, i + ".zip"), folderPath);
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
                Console.WriteLine(e);
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
            bool SVE3 = false;
            bool fRe = false;
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
                    long pos = 0;
                    all = reader.ReadInt32();
                    pos = reader.BaseStream.Position;
                    int controlData = 0;
                    int controlStruct = 0;
                    int controlPassword = 0;

                    try
                    {
                        string format = reader.ReadString();
                        //Console.WriteLine(format);
                        if (format == "SVE3")
                        {
                            SVE3 = true;
                        }
                    }
                    catch
                    {
                        SVE3 = false;
                    }

                    if (SVE3)
                    {
                        controlStruct = reader.ReadInt32();
                    }
                    else
                    {
                        reader.BaseStream.Position = pos;
                    }

                    string pass = "";
                    if (n != -1 && n <= all)
                    {
                        NEl = n;
                    }
                    using (BinaryReader reader1 = new BinaryReader(File.Open(Path.Combine(pathsave, Nfiledata), FileMode.Open)))
                    {
                        for (int i = 0; i < all; i++)
                        {
                            string str = reader.ReadString();
                            if (str == "p")
                            {
                                pass = reader1.ReadString().ToString();
                                using (SHA256 sha = SHA256.Create())
                                {
                                    byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                                    byte[] hash = sha.ComputeHash(bytes);

                                    StringBuilder sb = new StringBuilder();
                                    foreach (byte b in hash)
                                        sb.Append(b.ToString("x2"));

                                    controlPassword = BitConverter.ToInt32(hash, 0);

                                    if (pass != sb.ToString())
                                    {
                                        Console.WriteLine("[GDEError - 0001] Incorrect password");
                                        
                                        return null;
                                    }
                                }
                                i--;
                            }
                            if (SVE3 == true && fRe == false)
                            {
                                //Console.WriteLine("-");
                                controlData = reader1.ReadInt32();
                                if (controlData != controlStruct + controlPassword)
                                {
                                    Console.WriteLine("[GDEError - 0003] Checksum violation");
                                    return null;
                                }
                                fRe = true;
                            }
                            if (str == "l")
                            {
                                //Console.WriteLine("-");
                                int k = reader.ReadInt32();
                                //Console.WriteLine(k);
                                int[,] masdata = DESM.ReadMatrix(reader1);

                                for (int j = 0; j < masdata.GetLength(0); j++)
                                {
                                    DE.CreateCell(masdata[j, 0]);
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip1;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                    i++;
                                }
                                i--;
                                //i += k - 1;
                            }
                            if (str == "integer" || str == "i")
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
                            if (str == "doubl" || str == "d")
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
                            if (str == "str" || str == "s")
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
                            if (str == "booling" || str == "b")
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
                            if (str == "mas" || str == "m")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader1);
                                DE.CreateCell(masdata);
                                if (i == NEl)
                                {
                                    result = $"matrix_{i}";
                                    NEl++;
                                    if (NEl == all)
                                        NEl = 0;
                                    return result;
                                }
                            }
                            if (str == "files" || str == "f")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader1.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, i + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, i + ".zip"), folderPath);
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
            bool SVEO2 = false;
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
                    long pos = 0;
                    all = reader.ReadInt32();
                    pos = reader.BaseStream.Position;
                    int controlData = 0;
                    int controlStruct = 0;
                    int controlPassword = 0;

                    try
                    {
                        string format = reader.ReadString();
                        if (format == "SVEO2")
                        {
                            SVEO2 = true;
                        }
                    }
                    catch
                    {
                        SVEO2 = false;
                    }

                    if (SVEO2)
                    {
                        controlStruct = reader.ReadInt32();
                    }
                    else
                    {
                        reader.BaseStream.Position = pos;
                    }

                    string pass = "";
                    bool fPass = false;
                    if (n != -1 && n <= all)
                    {
                        NEl = n;
                    }
                    for (int i = 0; i < all; i++)
                    {
                        string str = reader.ReadString();
                        if (str == "p")
                        {
                            fPass = true;
                            i--;
                            continue;
                        }
                        if (str == "l")
                        {
                            int k = reader.ReadInt32();
                            for (int j = 0; j < k - 1; j++)
                            {
                                types.Add(str);
                            }
                            i += k - 1;
                        }
                        types.Add(str);
                    }
                    string razd = reader.ReadString();
                    if (razd == "/" || razd == "-/-/-/-/-/-")
                    {
                        if (fPass)
                        {
                            pass = reader.ReadString().ToString();

                            using (SHA256 sha = SHA256.Create())
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(DE.password);
                                byte[] hash = sha.ComputeHash(bytes);

                                StringBuilder sb = new StringBuilder();
                                foreach (byte b in hash)
                                    sb.Append(b.ToString("x2"));

                                controlPassword = BitConverter.ToInt32(hash, 0);

                                if (pass != sb.ToString())
                                {
                                    Console.WriteLine("[GDEError - 0001] Incorrect password");
                                    //Console.WriteLine(pass);
                                    //Console.WriteLine(sb.ToString());
                                    return null;
                                }
                            }
                        }
                        if (SVEO2)
                        {
                            controlData = reader.ReadInt32();
                            if (controlData != controlStruct + controlPassword)
                            {
                                Console.WriteLine("[GDEError - 0003] Checksum violation");
                                return null;
                            }
                        }
                        for (int i = 0; i < types.Count; i++)
                        {
                            //string str = reader.ReadString();
                            if (types[i] == "l")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader);
                                for (int j = 0; j < masdata.GetLength(0); j++)
                                {
                                    DE.CreateCell(masdata[j, 0]);                                    
                                    if (i == NEl)
                                    {
                                        result = DE.YList[i].tip1;
                                        NEl++;
                                        if (NEl == all)
                                            NEl = 0;
                                        return result;
                                    }
                                    i++;
                                }
                                i--;
                            }
                            if (types[i] == "integer" || types[i] == "i")
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
                            if (types[i] == "doubl" || types[i] == "d")
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
                            if (types[i] == "str" || types[i] == "s")
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
                            if (types[i] == "booling" || types[i] == "b")
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
                            if (types[i] == "files" || types[i] == "f")
                            {
                                int l = reader.ReadInt32();
                                if (l != 0)
                                {
                                    byte[] zipBytes = null;
                                    zipBytes = reader.ReadBytes(l);
                                    File.WriteAllBytes(Path.Combine(folderPath, i + ".zip"), zipBytes);
                                    //ВЫНОСИТСЯ АРХИВ В ПАПКУ КЭША
                                    string name = null;
                                    name = UnzipFile(Path.Combine(folderPath, i + ".zip"), folderPath);
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
                            if (types[i] == "mas" || types[i] == "m")
                            {
                                int[,] masdata = DESM.ReadMatrix(reader);
                                DE.CreateCell(masdata);
                                if (i == NEl)
                                {
                                    result = $"matrix_{i}";
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return NEl.ToString() + all.ToString();
        }
    }
}

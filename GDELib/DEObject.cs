using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GDELib
{
    public class DEObject
    {
        internal List<Yacheyka> YList= new List<Yacheyka>();
        //все данные
        private string path = "C:\\";
        private string pathcash; 
        private string Nstruct;
        private string Ndata;
        private DESaver DS;
        private bool TOne;

        public DEObject(string path_, bool _TOne = false, string _NameData = "data.sve", string _NameStruct = "struct.sve", string _pathcash = "pathcash")
        {
            //изменение данных
            path = path_;
            Nstruct = _NameStruct;
            Ndata = _NameData;
            TOne = _TOne;
            if(_pathcash == "pathcash")
            {
                _pathcash = Environment.CurrentDirectory;
            }
            pathcash = _pathcash;
            //события
            DS = new DESaver(this, path, Nstruct, Ndata, TOne, pathcash);
            if (Directory.Exists(Path.Combine(pathcash, "cashfile")) == false)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(pathcash, "cashfile"));
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message + "NoAccess");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        
        public void CreateCell(string types, dynamic data)
        {
            bool pr1 = false;
            string DATA = Convert.ToString(data);
            Yacheyka.type k = Yacheyka.type.integer; 
            if(types == "int")
            {
                k = Yacheyka.type.integer;
                pr1 = true;
            }
            if(types == "double")
            {
                k= Yacheyka.type.doubl;
                pr1 = true;
            }
            if (types == "string")
            {
                k = Yacheyka.type.str;
                pr1 = true;
            }
            if (types == "bool")
            {
                k = Yacheyka.type.booling;
                pr1 = true;
            }
            if(types == "file")
            {
                k = Yacheyka.type.files;
                pr1 = true;
            }
            if (pr1)
            {
                Yacheyka n = new Yacheyka(this, YList.Count, k, DATA);
                YList.Add(n);
                
            }
        }
        public void CreateCell(int[,] matrix)
        {
            bool pr1 = true;
            Yacheyka.type k = Yacheyka.type.mas;         
            if (pr1)
            {
                Yacheyka n = new Yacheyka(this, YList.Count, k, null, matrix);
                YList.Add(n);
            }
        }
        public void Dell(int i)
        {
            YList.RemoveAt(i);
            for(int j = 0; j < YList.Count; j++)
            {
                YList[j].Upd(i);
            }
        }
        public void Save()
        {
            if (TOne == false)
                DS.Save();
            else
                DS.SaveOne();
        }
        public string[] OpenAll()
        {
            if (TOne == false)
                return DS.OpenAll();
            else 
                return DS.OpenAllOne();
        }
        public string OpenNext(int i = -1)
        {
            if (TOne == false)
                return DS.OpenNext(i);
            else 
                return DS.OpenNextOne(i);
        }
        public void ResetData(int i = -1)
        {
            if(i == -1)
            {
                YList.Clear();
            }
            if(i != -1 && i > -1 && i < YList.Count)
            {
                YList.RemoveAt(i);
                for(int j = 0; j < YList.Count; j++)
                {
                    YList[j].i = j;
                }
            }
        }
        public string NumberInfo()
        {
            return YList.Count.ToString();
        }
        public void Association(bool _TOne)
        {
            TOne = _TOne;
        }
        public void CloneDataDE(DEObject DE)
        {
            DE.OpenAll();
            this.YList = DE.YList;
        }
        public void RecreateCell(int i ,string types, dynamic data)
        {
            if(i > -1 && i < YList.Count)
            {
                Yacheyka.type k = Yacheyka.type.integer;
                if (types == "int")
                {
                    k = Yacheyka.type.integer;
                }
                if (types == "double")
                {
                    k = Yacheyka.type.doubl;
                }
                if (types == "string")
                {
                    k = Yacheyka.type.str;
                }
                if (types == "bool")
                {
                    k = Yacheyka.type.booling;
                }
                if (types == "file")
                {
                    k = Yacheyka.type.files;
                }
                string DATA = Convert.ToString(data);
                YList[i] = new Yacheyka(this, i, k, DATA);
            }
        }
    }
}

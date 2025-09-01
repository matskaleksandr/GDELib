using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDELib
{
    internal class Yacheyka
    {
        public enum type
        {
            integer = 0,
            doubl = 1,
            str = 2,
            booling = 3,
            files = 4,
        }
        public int i = 0;
        public string tip1 = "0"; //int
        public string tip2 = "0"; //doble
        public string tip3 = ""; //string
        public bool tip4 = false; //bool
        public string tip5 = ""; //file (path)
        //соложные типы
        public type ya = type.integer;
        private DEObject DE;
        public Yacheyka(DEObject _DE, int j, type k, string autotip = "")
        { 
            DE = _DE;
            i = j;//ID
            if(k == type.integer)
            {
                tip1 = autotip;
            }
            if(k == type.doubl)
            {
                tip2 = autotip;
            }
            if (k == type.str)
            {
                tip3 = autotip;
            }
            if (k == type.booling)
            {
                tip4 = Convert.ToBoolean(autotip);
            }
            if(k == type.files)
            {
                tip5 = autotip;
            }
            ya = k;
        }
        public void Upd(int j)
        {
            i = j;
        }
    }
}

using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml.Serialization;
using SkladModel;
using AbstractModel;
using System;

namespace TestSklad
{
    class Program
    {



        static void Main(string[] args) 
        {
            SkladWrapper skladWrapper = new SkladWrapper(@"D:\39\skladConfig.xml");
            while (skladWrapper.Next())
            {
                Console.WriteLine(skladWrapper.objects.Count);
            }
            Console.ReadLine();
        }
    }
}

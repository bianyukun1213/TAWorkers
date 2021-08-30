using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Process
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "版本 " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine("本程序由环境 20-1 边宇琨制作。\n请在程序运行目录下创建 Source 文件夹，并将 .txt 文件置于其中。稍后，程序将读取 .txt 文件并在 Processed 文件夹下生成 .csv 文件。\n准备妥当后，输入任意字符继续。");
            Console.ReadLine();
            string path = Directory.GetCurrentDirectory();
            string sourcePath = path + "\\Source\\";
            string processedPath = path + "\\Processed\\";
            int count = 0;
            while (!Directory.Exists(sourcePath))
            {
                Console.WriteLine("Source 文件夹不存在。\n创建 Source 文件夹并置入 .txt 文件后，输入任意字符继续。");
                Console.ReadLine();
            }
            try
            {
                List<FileInfo> list = new DirectoryInfo(sourcePath).GetFiles("*.txt").ToList();
                foreach (var item in list)
                {
                    Console.WriteLine("正在处理 {0}。", item.Name);
                    StringBuilder sb = new StringBuilder(File.ReadAllText(item.FullName));
                    sb.Replace("$", ",");
                    Directory.CreateDirectory(processedPath);
                    File.WriteAllText(processedPath + item.Name.Replace(".txt", ".csv"), sb.ToString());
                    Console.WriteLine("{0} 处理完成。", item.Name);
                    count++;
                }
                Console.WriteLine("共处理 {0} 个文件。", count);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("使用表格处理程序打开这些 .csv 文件时，可能会提示它们并非 SYLK 文件，这是因为其内容的头两个字符为“ID”。该提示不影响 .csv 文件的导入。");
                Console.ResetColor();
                Console.WriteLine("输入任意字符退出。");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadLine();
            }
        }
    }
}

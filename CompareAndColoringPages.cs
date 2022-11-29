using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KrusDiffPdf.Models;
using NLog;

namespace KrusDiffPdf.Services
{
    public class CompareAndColoringPages
    {
        int numberPageFirst  = 0;
        int numberPageSecond = 0;
        private static Logger logger = LogManager.GetCurrentClassLogger();        
        public bool CompareFiles()
        {
            try
            {
                if (MainWindow.contentsFileOne.Pages.Count < MainWindow.contentsFileTwo.Pages.Count)
                {
                    ComparePages(MainWindow.contentsFileOne.Pages.Count - 1);
                }
                if (MainWindow.contentsFileTwo.Pages.Count < MainWindow.contentsFileOne.Pages.Count)
                {
                    ComparePages(MainWindow.contentsFileTwo.Pages.Count - 1);
                }
                if (MainWindow.contentsFileTwo.Pages.Count == MainWindow.contentsFileOne.Pages.Count)
                {
                    ComparePages(MainWindow.contentsFileOne.Pages.Count - 1);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                MessageBox.Show(ex.ToString());
                return false;
            }
            return true;
        }
        private static void FlashMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        private void ComparePages(int countPages)
        {
            CreateFolder();
            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(20))
            {
                var tasks = new List<Task>();
                MainWindow.contentsFileOne.ColoringPagePath = new List<string>();
                MainWindow.contentsFileTwo.ColoringPagePath = new List<string>();
                for (int i = 0; i <= countPages; i++)
                {
                    if (MainWindow.contentsFileOne.Pages[numberPageFirst].IsIgnor == true)
                    {
                        numberPageFirst++;
                        for (int j = numberPageFirst; j <= MainWindow.contentsFileOne.Pages.Count; j++)
                        {
                            if (j > countPages)
                            {
                                WaitTasks(tasks);
                                return;
                            }
                            if (MainWindow.contentsFileOne.Pages[j].IsIgnor == false)
                            {
                                numberPageFirst = j;
                                break;
                            }
                        }
                    }
                    if (MainWindow.contentsFileTwo.Pages[numberPageSecond].IsIgnor == true)
                    {
                        numberPageSecond++;
                        for (int k = numberPageSecond; k <= MainWindow.contentsFileTwo.Pages.Count; k++)
                        {
                            if (k > countPages)
                            {
                                WaitTasks(tasks);
                                return;
                            }
                            if (MainWindow.contentsFileTwo.Pages[k].IsIgnor == false)
                            {
                                numberPageSecond = k;
                                break;
                            }
                        }
                    }
                    var firstBmp = new Bitmap(MainWindow.contentsFileOne.Pages[numberPageFirst].PagePath);
                    var secondBmp = new Bitmap(MainWindow.contentsFileTwo.Pages[numberPageSecond].PagePath);
                    var pathFolderFirst = $@"{MainWindow.contentsFileOne.FolderPath}\{Path.GetFileName(MainWindow.contentsFileOne.Pages[numberPageFirst].PagePath)}";
                    var pathFolderSecond = $@"{MainWindow.contentsFileTwo.FolderPath}\{Path.GetFileName(MainWindow.contentsFileTwo.Pages[numberPageSecond].PagePath)}";
                    concurrencySemaphore.Wait();
                    var t = Task.Factory.StartNew(() =>
                    {
                        var comparing =  ComparePixel(
                        firstBmp,
                        secondBmp);
                        if (comparing == false)
                        {
                            ColoringPixel(firstBmp, secondBmp, pathFolderFirst, pathFolderSecond);
                        }
                        concurrencySemaphore.Release();
                    });
                    tasks.Add(t);
                    numberPageFirst++;
                    numberPageSecond++;
                    FlashMemory();
                    if (numberPageFirst > countPages || numberPageSecond > countPages)
                    {
                        break;
                    }
                }
                WaitTasks(tasks);
            }            
        }
        private void WaitTasks(List<Task> tasks)
        {
            Task.WaitAll(tasks.ToArray());
            MainWindow.contentsFileOne.ColoringPagePath = Directory.EnumerateFiles($@"{MainWindow.contentsFileOne.FolderPath}\", "*.png", SearchOption.TopDirectoryOnly).ToList();
            MainWindow.contentsFileTwo.ColoringPagePath = Directory.EnumerateFiles($@"{MainWindow.contentsFileTwo.FolderPath}\", "*.png", SearchOption.TopDirectoryOnly).ToList();
        }
        private bool ComparePixel(Bitmap firstBmp, Bitmap secondBmp)
        {
            bool flag = true;
            if (firstBmp.Width == secondBmp.Width
                && firstBmp.Height == secondBmp.Height)
            {
                for (int i = 0; i < firstBmp.Width; i++)
                {
                    for (int j = 0; j < secondBmp.Height; j++)
                    {
                        if (firstBmp.GetPixel(i, j) != secondBmp.GetPixel(i, j))
                        {
                            flag = false;
                            break;
                        }
                    }
                }
                if (flag == false)
                {
                    return false;
                }
                return true;
            }
            return true;
        }
        private void CreateFolder()
        {
            var setPathFirst = $@"{MainWindow.contentsFileOne.FolderPath}\ColoringPages";
            var setPathSecond = $@"{MainWindow.contentsFileTwo.FolderPath}\ColoringPages";
            MainWindow.contentsFileOne.FolderPath = setPathFirst;
            MainWindow.contentsFileTwo.FolderPath = setPathSecond;
            DirectoryInfo directoryOne = new DirectoryInfo(setPathFirst);
            DirectoryInfo directoryTwo = new DirectoryInfo(setPathSecond);
            if (directoryOne.Exists)
            {
                Directory.Delete(setPathFirst, true);
            }
            directoryOne.Create();
            if (directoryTwo.Exists)
            {
                Directory.Delete(setPathSecond, true);
            }
            directoryTwo.Create();
        }
        private void ColoringPixel(Bitmap firstPage, Bitmap secondPage, string pathFirst, string pathSecond)
        {
            var countAreasHorizontal = 20;
            var countAreasVertical = 20;
            var diffAreas = new List<DifferentArea>();

            var stepHorizontal = firstPage.Width / countAreasHorizontal;
            var stepVertical = secondPage.Height / countAreasVertical;

            var width = stepHorizontal * countAreasHorizontal;
            var height = stepVertical * countAreasVertical;            

            for (int horisont = 0; horisont < width; horisont += stepHorizontal)
            {                
                for (int vertical = 0; vertical < height; vertical += stepVertical)
                {
                    diffAreas.Add(new DifferentArea(
                        vertical,
                        vertical + stepVertical,
                        horisont,
                        horisont + stepHorizontal
                        ));
                }
            }
            var graphics1 = Graphics.FromImage(firstPage);
            var graphics2 = Graphics.FromImage(secondPage);
            Brush brush = new SolidBrush(Color.FromArgb(90, 250, 0, 0));
            foreach (var area in diffAreas)
            {
                var isAreaFill = false;
                for (var y = area.WidthStart; y < area.WidthEnd; y++)
                {
                    for (var x = area.HeightStart; x < area.HeightEnd; x++)
                    {
                        if (firstPage.GetPixel(x, y) != secondPage.GetPixel(x, y))
                        {
                            PointF[] points =
                            {
                                new PointF(area.HeightStart,area.WidthStart),
                                new PointF(area.HeightStart,area.WidthEnd ),
                                new PointF(area.HeightEnd,area.WidthEnd ),
                                new PointF(area.HeightEnd,area.WidthStart),
                            };
                            graphics2.FillPolygon(brush, points);
                            graphics1.FillPolygon(brush, points);
                            isAreaFill = true;
                        }
                        if (isAreaFill) break;
                    }
                    if (isAreaFill) break;
                }
            }
            firstPage.Save(pathFirst, ImageFormat.Png);
            secondPage.Save(pathSecond, ImageFormat.Png);
            firstPage.Dispose();
            secondPage.Dispose();
        }
    }
}
namespace SaveWeather
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using global::SaveWeather.BLL;
    using Microsoft.Extensions.Configuration;
    using MIL.Html;

    internal class SaveWeather
    {
        public static IConfiguration Configuration { get; set; }

        private static void Main(string[] args)
        {
            LoadingConfiguration();

            bool isOffLineMode = false;
            Boolean.TryParse(Configuration["offLineMode"], out isOffLineMode);

            string html;

            if (isOffLineMode)
            {
                var inputFile = Configuration["inputFile"];
                html = OpenFile(inputFile);
            }
            else
            {
                html = FetchWebPage();
            }
            if (!html.Equals(String.Empty))
            {
                if (!isOffLineMode)
                {
                    WriteLog(html);
                }
                List<Meres> MeresList = GetData(html);
                SaveData(MeresList);
            }

            bool isBatchMode = false;
            Boolean.TryParse(Configuration["batchMode"], out isBatchMode);

            if (!isBatchMode)
            {
                Console.Read();
            }
        }

        private static void LoadingConfiguration()
        {
            var builder = new ConfigurationBuilder()
                             .SetBasePath(Directory.GetCurrentDirectory())
                             .AddJsonFile("omsz_appsettings.json");

            Configuration = builder.Build();
        }

        private static string OpenFile(string fileName)
        {
            string html = string.Empty;
            using (StreamReader sr = new StreamReader(fileName, Encoding.UTF8))
            {
                html = sr.ReadToEnd();
                sr.Close();
            }
            if (html == string.Empty)
            {
                Console.WriteLine("Nothing in the file");
            }
            return html;
        }

        private static string FetchWebPage()
        {
            HttpWebResponse response = null;

            StringBuilder sb = new StringBuilder();

            byte[] buf = new byte[8192];

            var url = Configuration["url"];

            HttpWebRequest request = (HttpWebRequest)
                WebRequest.Create(url);

            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Hiba az oldal elérésében: {0}", ex.Message + " " + ex.StackTrace);
                WriteErrorLog(string.Format("Hiba az oldal elérésében: {0}", ex.Message + " " + ex.StackTrace));
                return String.Empty;
            }

            Stream resStream = response.GetResponseStream();

            string tempString = null;
            int count = 0;

            do
            {
                count = resStream.Read(buf, 0, buf.Length);

                if (count != 0)
                {
                    tempString = Encoding.UTF8.GetString(buf, 0, count);

                    sb.Append(tempString);
                }
            }
            while (count > 0);

            return sb.ToString();
        }

        private static List<Meres> GetData(string html)
        {
            List<Meres> MeresList = new List<Meres>();

            if (html.Equals(string.Empty))
                return MeresList;

            html = html.Replace("rbg1", "rbg0");
            html = html.Replace("T rbg0", "rbg0");
            html = html.Replace("Wikon rbg0", "rbg0");
            html = html.Replace("Wd rbg0", "rbg0");
            html = html.Replace("Wf rbg0", "rbg0");
            html = html.Replace("P rbg0", "rbg0");
            html = html.Replace("H rbg0", "rbg0");
            html = html.Replace("R rbg0", "rbg0");

            HtmlDocument mDocument = HtmlDocument.Create(html, false);
            HtmlNodeCollection tdcoll = mDocument.Nodes.FindByAttributeNameValue("class", "rbg0", true);

            List<DateTime> dateList = GetDateTimeList(mDocument);

            int index = 0;
            int rowIndex = 0;
            string idopont = String.Empty;
            string homerseklet = String.Empty;
            string legnyomas = String.Empty;
            string csapadek = String.Empty;
            DateTime dte = DateTime.Now;
            foreach (HtmlElement td in tdcoll)
            {
                if (index % 9 == 0)
                {
                    // in UTC
                    dte = dateList[rowIndex];
                    rowIndex++;
                }
                if (index % 9 == 1)
                {
                    homerseklet = ((HtmlElement)td.FirstChild).Text;
                }

                if (index % 9 == 6)
                {
                    legnyomas = ((HtmlElement)td.FirstChild).Text;
                }

                if (index % 9 == 8)
                {
                    csapadek = ((HtmlElement)td.FirstChild).Text;
                    csapadek = csapadek.Replace('.', ',');
                    csapadek = csapadek.Replace("-", "0,0");
                }

                if (index % 9 == 8)
                {
                    Meres meres = new Meres();
                    meres.Datum = dte;
                    try
                    {
                        meres.Homerseklet = Convert.ToInt32(homerseklet);
                        meres.Legnyomas = Convert.ToInt32(legnyomas);
                        meres.Csapadek = Convert.ToDouble(csapadek);
                        MeresList.Add(meres);
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = $"{ex.Message} \n {ex.StackTrace}";
                        WriteErrorLog(errorMessage);
                        continue;
                    }
                }
                index++;
            }
            return MeresList;
        }

        private static List<DateTime> GetDateTimeList(HtmlDocument mDocument)
        {
            List<DateTime> dateTimeList = new List<DateTime>();

            HtmlNodeCollection optionColl = mDocument.Nodes.FindByName("option");

            DateTime convertedDateTime;

            foreach (var option in optionColl)
            {
                if (TryParseDateTime((option as HtmlElement).Text, out convertedDateTime))
                {
                    dateTimeList.Add(convertedDateTime);
                }
            }

            return dateTimeList;
        }

        private static bool TryParseDateTime(string p_idopont, out DateTime parsedDateTime)
        {
            bool isSuccess = true;
            parsedDateTime = DateTime.MinValue;

            if (p_idopont.Length < 30)
            {
                isSuccess = false;
                return isSuccess;
            }

            string[] tmp = p_idopont.Split(" ".ToCharArray());
            tmp[0] = tmp[0].Substring(0, 4);                         //year
            tmp[1] = tmp[1];                                        //month
            tmp[2] = tmp[2].Substring(0, tmp[2].Length - 1);        //day
            tmp[5] = tmp[5].Substring(1, 2);                         //hour

            int year = 0;
            int month = 0;
            int day = 0;
            int hour = 0;

            try
            {
                year = Int32.Parse(tmp[0]);
                month = tmp[1].ToInt();
                day = Int32.Parse(tmp[2]);
                hour = Int32.Parse(tmp[5]);
                parsedDateTime = new DateTime(year, month, day);
                parsedDateTime = parsedDateTime.AddHours(hour);
            }
            catch (Exception)
            {
                isSuccess = false;
            }
            return isSuccess;
        }

        private static void WriteLog(string html)
        {
            var dateStamp = DateTime.Now.ToString("yyyyMMdd");
            var logFile = $"{Configuration["logFile"]}{dateStamp}.txt";

            Thread.CurrentThread.CurrentCulture = new CultureInfo("hu-HU");

            using (StreamWriter swlog = new StreamWriter(logFile, true, Encoding.Default))
            {
                swlog.Write(html);
            }
        }

        private static int GetCountNum(List<Meres> MeresList)
        {
            int count = 0;
            foreach (Meres m in MeresList)
            {
                if (m.Datum.Day == DateTime.Now.Day)
                {
                    switch (m.Datum.Hour)
                    {
                        case 0:
                        case 6:
                        case 12:
                        case 18:
                            count++;
                            break;
                    }
                }
            }
            return count;
        }

        private static double AvgHom(List<Meres> MeresList)
        {
            double szumm_hom = 0;
            int count = 0;
            foreach (Meres m in MeresList)
            {
                if (m.Datum.Day == DateTime.Now.Day)
                {
                    switch (m.Datum.Hour)
                    {
                        case 0:
                        case 6:
                        case 12:
                        case 18:
                            szumm_hom += m.Homerseklet;
                            count++;
                            break;
                    }
                }
            }
            return szumm_hom / count;
        }

        private static double AvgLegnyom(List<Meres> MeresList)
        {
            double szumm_legnyom = 0;
            int count = 0;
            foreach (Meres m in MeresList)
            {
                if (m.Datum.Day == DateTime.Now.Day)
                {
                    switch (m.Datum.Hour)
                    {
                        case 0:
                        case 6:
                        case 12:
                        case 18:
                            szumm_legnyom += m.Legnyomas;
                            count++;
                            break;
                    }
                }
            }
            return szumm_legnyom / count;
        }

        private static double SummCsapadek(List<Meres> MeresList)
        {
            double szumm_csapadek = 0;
            foreach (Meres m in MeresList)
            {
                szumm_csapadek += m.Csapadek;
            }
            return szumm_csapadek;
        }

        private static void SaveData(List<Meres> MeresList)
        {
            var outPutFile = Configuration["outputFile"];

            double avg_hom, avg_legnyom, szumm_csapadek;
            int count = GetCountNum(MeresList);
            if (count != 4)
            {
                Console.WriteLine("Hiba: Csak {0} mérés van!", count);
                WriteErrorLog(string.Format("Hiba: Csak {0} mérés van!", count));
                MinMaxs(MeresList);
                return;
            }
            DateTime today = DateTime.Now;
            avg_hom = AvgHom(MeresList);
            avg_legnyom = AvgLegnyom(MeresList);
            szumm_csapadek = SummCsapadek(MeresList);
            StreamWriter sw = new StreamWriter(outPutFile, true, Encoding.Default);
            try
            {
                if (szumm_csapadek > 0.4)
                    sw.WriteLine("{0};{1};{2};{3:f0};{4:f0};{5:f0}", today.Year, today.Month, today.Day, avg_hom, avg_legnyom, szumm_csapadek);
                else
                    sw.WriteLine("{0};{1};{2};{3:f0};{4:f0};", today.Year, today.Month, today.Day, avg_hom, avg_legnyom);
                sw.Flush();
                sw.Close();
                StreamReader sr = new StreamReader(outPutFile, Encoding.Default);
                Console.Write(sr.ReadToEnd());
                sr.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Hiba a mentésben: {0}", ex.Message + " " + ex.StackTrace);
                WriteErrorLog(string.Format("Hiba a mentésben: {0}", ex.Message + " " + ex.StackTrace));
                sw.Close();
            }
            MinMaxs(MeresList);
        }

        private static void MinMaxs(List<Meres> MeresList)
        {
            int min_press = MeresList[0].Legnyomas;
            int max_press = MeresList[0].Legnyomas;
            int min_temp = MeresList[0].Homerseklet;
            int max_temp = MeresList[0].Homerseklet;
            double summ_csapadek = SummCsapadek(MeresList);

            foreach (Meres m in MeresList)
            {
                if (m.Homerseklet < min_temp)
                    min_temp = m.Homerseklet;
                if (m.Homerseklet > max_temp)
                    max_temp = m.Homerseklet;

                if (m.Legnyomas < min_press)
                    min_press = m.Legnyomas;
                if (m.Legnyomas > max_press)
                    max_press = m.Legnyomas;
            }
            Console.WriteLine("\n\t\t Min. \t Max.");
            Console.WriteLine("Hőmérséklet\t {0} \t {1}", min_temp, max_temp);
            Console.WriteLine("Légnyomás\t {0} \t {1}", min_press, max_press);
            if (summ_csapadek > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Csapadék\t  \t {0}", summ_csapadek);
            }
        }

        private static void WriteErrorLog(string message)
        {
            var dateStamp = DateTime.Now.ToString("yyyyMMdd");
            var errorLogFile = $"{Configuration["errorLogFile"]}{dateStamp}.txt";

            using (StreamWriter sw = new StreamWriter(errorLogFile, true, Encoding.Default))
            {
                sw.WriteLine(message);
            }
        }
    }

    #region Extension methods

    public static class Month
    {
        public static int ToInt(this string month)
        {
            return Array.IndexOf(CultureInfo.CurrentCulture.DateTimeFormat.MonthNames, month.ToLower(CultureInfo.CurrentCulture)) + 1;
        }
    }

    #endregion Extension methods
}